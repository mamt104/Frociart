# Runtime di supporto ai programmi Python esportati da Frociart.
# Non contiene un interprete di diagrammi, eval o codice scaricato dalla rete.
import builtins as _fc_builtins
import decimal as _fc_decimal
import math as _fc_math
import os as _fc_os
import pathlib as _fc_pathlib
import random as _fc_random
import re as _fc_re
import sys as _fc_sys
import struct as _fc_struct
import tempfile as _fc_tempfile


# Invariant Unicode spaces used by the app; Python strip() also removes C0 separators.
_fc_input_spaces = ''.join(chr(c) for c in [*range(9, 14), 32, 0x85, 0xa0, 0x1680, *range(0x2000, 0x200b), 0x2028, 0x2029, 0x202f, 0x205f, 0x3000])


def _fc_trim_input(value):
    return value.strip(_fc_input_spaces)


def _fc_format_number(value):
    if value == 0:
        return '0'
    if abs(value) < 1e15 and value.is_integer():
        return str(int(value))
    numerator, denominator = abs(value).as_integer_ratio()
    scale = denominator.bit_length() - 1
    digits = str(numerator * 5 ** scale)
    exponent = len(digits) - scale - 1
    if len(digits) > 15:
        digits = str(int(digits[:15]) + (digits[15] >= '5'))
        if len(digits) > 15:
            exponent += 1
            digits = digits[:15]
    digits = digits.rstrip('0')
    if exponent >= 15 or exponent < -4:
        body = digits[0] + ('.' + digits[1:] if len(digits) > 1 else '') + 'E' + ('+' if exponent >= 0 else '-') + str(abs(exponent)).zfill(2)
    else:
        at = exponent + 1
        body = '0.' + '0' * (-at) + digits if at <= 0 else digits + '0' * (at - len(digits)) if at >= len(digits) else digits[:at] + '.' + digits[at:]
    return ('-' if value < 0 else '') + body


class _fc_Error(Exception):
    pass


class _fc_ArrayCell:
    # Values remain immutable. Only explicit reference arguments share a cell.
    def __init__(self, value, kind=0):
        self.value = value
        self.kind = kind


class _fc_ArrayArgument:
    def __init__(self, cell):
        self.cell = cell
        self.snapshot = cell.value


class _fc_Runtime:
    unset = object()

    def __init__(self, reader=None, writer=None, file_reader=None, file_saver=None):
        self.reader = reader or input
        self.writer = writer
        self.file_reader = file_reader or self._read_path
        self.file_saver = file_saver or self._save_path
        self.steps = 0
        self.max_steps = 10000
        self.frames = []
        self.references = False
        self.last_variables = {}
        self.error_variables = None
        self.output = []
        self.output_newlines = []
        self.output_size = 0
        self.read_name = self.read_text = None
        self.read_position = 0
        self.write_name = None
        self.write_parts = []
        self.write_size = self.write_bytes = 0
        self.recovery_path = None
        self.x = self.y = 0.0
        self.heading = 90.0
        self.color = '#000000'
        self.segments = []
        self.turtle_used = False

    @staticmethod
    def units(text):
        return len(text.encode('utf-16-le', errors='surrogatepass')) // 2

    @staticmethod
    def real(bits):
        return _fc_struct.unpack('>d', bits.to_bytes(8, 'big'))[0]

    @staticmethod
    def number(value):
        if type(value) not in (int, float):
            raise _fc_Error('Serve un numero.')
        try:
            value = float(value)
        except (ValueError, OverflowError):
            raise _fc_Error('Il numero non è finito.') from None
        if not _fc_math.isfinite(value):
            raise _fc_Error('Il numero non è finito.')
        return value

    def text(self, value):
        if not isinstance(value, str):
            raise _fc_Error('Serve un testo.')
        if self.units(value) > 32768:
            raise _fc_Error('Il testo supera 32768 caratteri.')
        return value

    def truth(self, value):
        if type(value) is bool:
            return value
        return self.number(value) != 0

    def value(self, value, name):
        if value is not self.unset:
            return value
        if name.lower() == 'pi' or name == 'π':
            return _fc_math.pi
        raise _fc_Error('La variabile «' + name + '» non ha ancora un valore.')

    def format(self, value):
        if value is None:
            return ''
        if type(value) is bool:
            return 'vero' if value else 'falso'
        if isinstance(value, str):
            return value
        if isinstance(value, tuple):
            def element(v):
                if isinstance(v, str):
                    return '"' + v.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\r', '\\r').replace('\t', '\\t') + '"'
                return self.format(v)
            return '[' + ', '.join(element(v) for v in value) + ']'
        n = self.number(value)
        return _fc_format_number(n)

    def scalar_kind(self, value):
        if type(value) is bool:
            return 1
        if isinstance(value, str):
            self.text(value)
            return 2
        self.number(value)
        return 0

    def vector(self, values):
        values = tuple(values)
        if len(values) > 1024:
            raise _fc_Error('Un vettore può contenere al massimo 1024 elementi.')
        kinds = {self.scalar_kind(v) for v in values}
        if len(kinds) > 1:
            raise _fc_Error('Gli elementi del vettore devono avere lo stesso tipo.')
        # The native constructor accounts for two separator characters per item, including the last.
        if self.units(self.format(values)) + (2 if values else 0) > 32768:
            raise _fc_Error('Il contenuto del vettore supera 32768 caratteri.')
        return values

    @staticmethod
    def require_vector(value):
        if not isinstance(value, tuple):
            raise _fc_Error('Serve un vettore.')
        return value

    def integer(self, value, minimum, maximum, label='Il valore'):
        n = self.number(value)
        if n != _fc_math.trunc(n) or n < minimum or n > maximum:
            raise _fc_Error(label + ' deve essere un intero da ' + str(minimum) + ' a ' + str(maximum) + '.')
        return int(n)

    def index(self, vector, value):
        self.require_vector(vector)
        index = self.integer(value, 0, 2147483647, 'L’indice')
        if index >= len(vector):
            raise _fc_Error('Indice fuori dal vettore.')
        return index

    def at(self, vector, index_expression):
        self.require_vector(vector)  # Validate the target before evaluating an index with side effects.
        return vector[self.index(vector, index_expression())]

    def at_name(self, name, index_expression):
        self.require_vector(self.lookup(name))
        index = index_expression()
        vector = self.lookup(name)  # The index may call a function that changes this vector.
        return vector[self.index(vector, index)]

    def set_after_name(self, value, name, index_expression):
        self.require_vector(self.lookup(name))
        frame = self.frames[-1]
        frame['assignment'] = value
        try:
            index = index_expression()
            return self.set_after(value, self.lookup(name), lambda: index)
        finally:
            frame['assignment'] = self.unset

    def set_after(self, value, vector, index_expression):
        self.require_vector(vector)  # The caller evaluates the RHS before the target and index.
        frame = self.frames[-1] if self.frames else None
        if frame is not None:
            frame['assignment'] = value
        try:
            index = self.index(vector, index_expression())
        finally:
            if frame is not None:
                frame['assignment'] = self.unset
        if self.scalar_kind(vector[index]) != self.scalar_kind(value):
            raise _fc_Error('Il nuovo valore deve avere lo stesso tipo del vettore.')
        return self.vector(vector[:index] + (value,) + vector[index + 1:])

    def concat(self, a, b):
        return self.text(self.format(a) + self.format(b))

    def add(self, a, b):
        if isinstance(a, str) or isinstance(b, str):
            return self.text(self.format(a) + self.format(b))
        return self.number(self.number(a) + self.number(b))

    def sub(self, a, b):
        return self.number(self.number(a) - self.number(b))

    def mul(self, a, b):
        return self.number(self.number(a) * self.number(b))

    def div(self, a, b):
        a, b = self.number(a), self.number(b)
        if b == 0:
            raise _fc_Error('Divisione per zero.')
        return self.number(a / b)

    def mod(self, a, b):
        a, b = self.number(a), self.number(b)
        if b == 0:
            raise _fc_Error('Resto con divisore zero.')
        return self.number(_fc_math.fmod(a, b))

    def power(self, a, b):
        a, b = self.number(a), self.number(b)
        try:
            return self.number(_fc_math.pow(a, b))
        except (ValueError, OverflowError):
            raise _fc_Error('Potenza non definita o risultato troppo grande.') from None

    def equal(self, a, b):
        if type(a) is bool or type(b) is bool:
            return type(a) is bool and type(b) is bool and a == b
        if isinstance(a, tuple) or isinstance(b, tuple):
            return isinstance(a, tuple) and isinstance(b, tuple) and len(a) == len(b) and all(self.equal(x, y) for x, y in zip(a, b))
        if isinstance(a, str) or isinstance(b, str):
            return isinstance(a, str) and isinstance(b, str) and a == b
        return self.number(a) == self.number(b)

    def compare(self, a, b, op):
        if isinstance(a, str) and isinstance(b, str):
            a, b = a.encode('utf-16-be', 'surrogatepass'), b.encode('utf-16-be', 'surrogatepass')
        else:
            a, b = self.number(a), self.number(b)
        if op == '<':
            return a < b
        if op == '<=':
            return a <= b
        if op == '>':
            return a > b
        return a >= b

    def check_type(self, value, kind, array=False):
        if array:
            for item in self.require_vector(value):
                self.check_type(item, kind)
            return value
        valid = not isinstance(value, tuple) and value is not None and value is not self.unset
        if kind == 1:
            valid = type(value) in (int, float) and self.number(value) == _fc_math.trunc(value)
        elif kind == 2:
            valid = type(value) in (int, float)
        elif kind == 3:
            valid = isinstance(value, str)
        elif kind == 4:
            valid = type(value) is bool
        elif kind == 5:
            valid = False
        if not valid:
            raise _fc_Error('Il valore non corrisponde al tipo dichiarato.')
        self.scalar_kind(value)
        return value

    def convert(self, value, declaration=None, parameter=None):
        if declaration is not None:
            kind, array, length = declaration
            def scalar(v):
                if kind == 1 and type(v) in (int, float):
                    return float(_fc_math.trunc(self.number(v)))
                return self.check_type(v, kind)
            if array:
                if len(self.require_vector(value)) != length:
                    raise _fc_Error('La dimensione del vettore dichiarato non può cambiare.')
                value = self.vector(scalar(v) for v in value)
            else:
                value = scalar(value)
        if parameter is not None:
            value = self.check_type(value, parameter[0], parameter[1])
        return value

    def initial(self, kind, array=False, length=0):
        value = '' if kind == 3 else False if kind == 4 else 0.0
        return self.vector([value] * self.integer(length, 0, 1024, 'La dimensione')) if array else value

    def parse_input(self, raw, text=False):
        raw = self.text(raw)
        if text:
            return raw
        trimmed = _fc_trim_input(raw)
        numeric = trimmed.rstrip("\0").rstrip("\t\n\v\f\r ")
        if _fc_re.fullmatch(r'[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?', numeric):
            value = float(numeric.replace(',', '.'))
            if not _fc_math.isfinite(value) and numeric != trimmed:
                return raw
            return self.number(value)
        if trimmed.lower() in ('true', 'vero'):
            return True
        if trimmed.lower() in ('false', 'falso'):
            return False
        if trimmed.lower() in ('nan', 'infinity', '+infinity', '-infinity', 'infinito'):
            raise _fc_Error('Inserisci un numero finito.')
        return raw

    @staticmethod
    def wants_text(declaration, parameter, indexed):
        spec = declaration if declaration is not None else parameter
        return spec is not None and spec[0] == 3 and (indexed or not spec[1])

    def read(self, name, prompt='', declaration=None, parameter=None, vector=None, index_expression=None):
        self.guard()
        variable_name = name
        index = None
        if index_expression is not None:
            self.require_vector(vector)
            position = index_expression()
            index = self.index(self.lookup(variable_name) if self.references else vector, position)
            name += '[' + str(index) + ']'
        self.guard()
        if not prompt.strip() or prompt == 'Inserisci un valore per x':
            prompt = 'Inserisci un valore per ' + name
        while True:
            try:
                raw = self.reader(prompt + ': ')
            except EOFError:
                raise _fc_Error('Input terminato prima di ricevere un valore valido.') from None
            try:
                value = self.parse_input(raw, self.wants_text(declaration, parameter, index is not None))
                if index is not None:
                    value = self.set_after(value, self.lookup(variable_name) if self.references else vector, lambda: float(index))
                value = self.convert(value, declaration, parameter)
                self.accept_input(variable_name, value, index is not None)
                return value
            except _fc_Error as error:
                print(str(error), file=_fc_sys.stderr)

    def guard(self):
        if self.steps >= self.max_steps:
            raise _fc_Error('Limite di esecuzione raggiunto. Controlla la condizione del ciclo.')

    def step(self):
        self.guard()
        self.steps += 1

    def test(self, expression):
        self.step()
        return self.truth(expression())

    def enter(self, scope, parameters=None, references=False):
        self.guard_call()
        self.references = self.references or references
        values, bindings = {}, {}
        for name, value in (parameters or {}).items():
            if isinstance(value, _fc_ArrayArgument):
                bindings[name] = value.cell
                value = value.cell.value
            values[name] = value
        self.frames.append({'scope': scope, 'variables': values, 'bindings': bindings,
                            'overlays': {}, 'new_arrays': {}, 'whole_writes': set(),
                            'transaction': None, 'retained': {}, 'assignment': self.unset})

    def lookup(self, name):
        frame = self.frames[-1]
        values = frame['transaction'] if frame['transaction'] is not None else frame['variables']
        return self.value(values.get(name, self.unset), name)

    def reference(self, name, declaration=None, parameter=None):
        frame = self.frames[-1]
        value = self.require_vector(self.lookup(name))
        spec = declaration if declaration is not None else parameter
        kind = spec[0] if spec is not None else 0
        if frame['transaction'] is not None and (name in frame['whole_writes'] or name not in frame['variables']):
            if name not in frame['new_arrays']:
                frame['new_arrays'][name] = _fc_ArrayCell(value, kind)
            return _fc_ArrayArgument(frame['new_arrays'][name])
        if name not in frame['bindings']:
            frame['bindings'][name] = _fc_ArrayCell(self.require_vector(frame['variables'][name]), kind)
        cell = frame['bindings'][name]
        if frame['transaction'] is not None:
            if cell in frame['overlays']:
                return _fc_ArrayArgument(frame['overlays'][cell])
            if value is not cell.value:
                return _fc_ArrayArgument(self.overlay(frame, cell, value))
        return _fc_ArrayArgument(cell)

    def check_reference(self, argument, kind):
        if not isinstance(argument, _fc_ArrayArgument):
            raise _fc_Error('Parametro per riferimento non valido.')
        self.check_type(argument.snapshot, kind, True)
        cell = argument.cell
        if cell.kind != 0 and kind != 0 and cell.kind != kind:
            raise _fc_Error('Il tipo del vettore per riferimento deve coincidere con quello originale.')
        self.check_type(cell.value, kind, True)
        if cell.kind == 0 and kind != 0:
            cell.kind = kind
        return argument

    @staticmethod
    def overlay(frame, cell, value):
        if cell not in frame['overlays']:
            frame['overlays'][cell] = _fc_ArrayCell(value, cell.kind)
        return frame['overlays'][cell]

    def propagate(self, cell):
        for frame in self.frames:
            transaction = frame['transaction']
            for name, bound in frame['bindings'].items():
                if bound is cell:
                    frame['variables'][name] = cell.value
                    if transaction is not None and name not in frame['whole_writes']:
                        transaction[name] = frame['overlays'].get(cell, cell).value
            if transaction is not None:
                for name, bound in frame['new_arrays'].items():
                    if bound is cell:
                        transaction[name] = cell.value
                for parent, overlay in frame['overlays'].items():
                    if overlay is cell:
                        for name, bound in frame['bindings'].items():
                            if bound is parent and name not in frame['whole_writes']:
                                transaction[name] = cell.value

    def check_cell(self, cell, value):
        if len(self.require_vector(value)) != len(cell.value):
            raise _fc_Error('Un vettore per riferimento conserva la dimensione originale.')
        self.check_type(value, cell.kind, True)

    def guard_call(self):
        if len(self.frames) >= 128:
            raise _fc_Error('Troppe chiamate annidate: massimo 128 livelli.')

    def leave(self):
        values = dict(self.frames[-1]['variables'])
        if self.error_variables is None and _fc_sys.exc_info()[0] is not None:
            self.error_variables = values
        if self.frames[-1]['scope'] is None:
            self.last_variables = values
        self.frames.pop()

    def value_size(self, value):
        if isinstance(value, _fc_ArrayArgument):
            return self.value_size(value.snapshot)
        if isinstance(value, str):
            return self.units(value)
        if isinstance(value, tuple):
            return len(value) * 8 + sum(self.units(v) for v in value if isinstance(v, str))
        return 8

    def check_memory(self):
        size, slots = self.output_size, 0
        for frame in self.frames:
            for values in (frame['variables'], frame['transaction']):
                if values is not None:
                    slots += len(values)
                    size += sum(self.value_size(v) for v in values.values())
            for values in frame['retained'].values():
                size += sum(self.value_size(v) for v in values)
            if frame['assignment'] is not self.unset:
                size += self.value_size(frame['assignment'])
            if size > 2097152 or slots > 16384:
                raise _fc_Error('La memoria dei valori supera il limite di esecuzione. Riduci i dati conservati o le chiamate ricorsive.')

    def begin(self):
        frame = self.frames[-1]
        frame['transaction'] = dict(frame['variables'])
        if self.references:
            frame['overlays'].clear()
            frame['new_arrays'].clear()
            frame['whole_writes'].clear()

    def stage(self, name, value, check=True, indexed=False):
        frame = self.frames[-1]
        if not self.references:
            frame['transaction'][name] = value
        elif not indexed:
            frame['transaction'][name] = value
            frame['whole_writes'].add(name)
            frame['new_arrays'].pop(name, None)
        elif name in frame['whole_writes'] and name in frame['new_arrays']:
            cell = frame['new_arrays'][name]
            self.check_cell(cell, value)
            cell.value = value
            self.propagate(cell)
        elif name not in frame['whole_writes'] and name in frame['bindings']:
            cell = self.overlay(frame, frame['bindings'][name], frame['transaction'][name])
            self.check_cell(cell, value)
            cell.value = value
            self.propagate(cell)
        else:
            frame['transaction'][name] = value
        if check:
            self.check_memory()
        return value

    def commit(self, check=False):
        if check:
            self.check_memory()
        frame = self.frames[-1]
        if self.references:
            for parent, overlay in frame['overlays'].items():
                self.check_cell(parent, overlay.value)
            for cell in frame['new_arrays'].values():
                self.check_cell(cell, cell.value)
            for name in frame['whole_writes']:
                frame['bindings'].pop(name, None)
            for parent, overlay in frame['overlays'].items():
                parent.value = overlay.value
                self.propagate(parent)
            frame['bindings'].update(frame['new_arrays'])
        frame['variables'], frame['transaction'] = frame['transaction'], None
        frame['overlays'].clear()
        frame['new_arrays'].clear()
        frame['whole_writes'].clear()

    def accept_input(self, name, value, indexed=False):
        self.begin()
        try:
            self.stage(name, value, indexed=indexed)
            self.commit()
        finally:
            self.frames[-1]['transaction'] = None

    def hold(self, key, value):
        self.frames[-1]['retained'].setdefault(key, []).append(value)
        return value

    def release(self, key, value):
        self.frames[-1]['retained'].pop(key, None)
        return value

    def call(self, key, function, *arguments):
        value = function(*arguments)
        self.release(key, value)
        self.hold(-1, value)
        try:
            self.check_memory()
        finally:
            self.release(-1, value)
        return value

    def emit(self, value, newline=True):
        text = self.format(value)
        size = self.units(text)
        if self.output_size + size > 262144:
            raise _fc_Error('Il risultato supera 256 KiB.')
        self.output.append(text)
        self.output_newlines.append(newline)
        self.output_size += size
        if self.writer is None:
            print(text, end="\n" if newline else "", flush=True)
        else:
            self.writer(text)  # Optional observer receives each print event, as before.

    def round(self, value, digits=0.0):
        value = self.number(value)
        digits = self.integer(digits, 0, 15, 'Le cifre decimali')
        if abs(value) >= 1e16:
            return value
        factor = 10.0 ** digits
        scaled = value * factor
        absolute = abs(scaled)
        whole = _fc_math.floor(absolute)
        if absolute - whole >= 0.5:
            whole += 1
        return _fc_math.copysign(whole, scaled) / factor

    def intrinsic(self, name, *args):
        # These are value operations only; graph control flow lives in the exported Python functions.
        if name == 'eof':
            return self.eof()
        a = args[0]
        if name in ('array', 'vettore'):
            length = self.integer(a, 0, 1024, 'La dimensione')
            initial = args[1] if len(args) == 2 else 0.0
            self.scalar_kind(initial)
            return self.vector([initial] * length)
        if name in ('len', 'size'):
            if isinstance(a, tuple):
                return float(len(a))
            if name == 'len':
                return float(len(self.characters(a)))
            raise _fc_Error('size richiede un vettore.')
        if name in ('char', 'tocode'):
            chars = self.characters(a)
            if name == 'tocode':
                if len(chars) != 1:
                    raise _fc_Error('tocode richiede un solo carattere.')
                return float(ord(chars))
            index = self.integer(args[1], 0, 2147483647, 'L’indice')
            if index >= len(chars):
                raise _fc_Error('Indice fuori dal testo.')
            return chars[index]
        if name in ('tointeger', 'toreal'):
            value = _fc_trim_input(self.text(a))
            pattern = r'[+-]?[0-9]+' if name == 'tointeger' else r'[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?'
            if not _fc_re.fullmatch(pattern, value):
                raise _fc_Error('Il testo non contiene un numero valido.')
            value = self.number(float(value.replace(',', '.')))
            return float(self.integer(value, -2147483648, 2147483647)) if name == 'tointeger' else value
        if name == 'int' and len(args) == 2:
            if not isinstance(args[1], str) or args[1].lower() != 'passo':
                raise _fc_Error('Il secondo argomento di int deve essere "passo".')
            value = self.integer(a, -2147483648, 2147483647, 'Il passo')
            if value == 0:
                raise _fc_Error('Il passo del ciclo deve essere diverso da zero.')
            return float(value)
        if name in ('sum', 'somma') or name in ('min', 'max') and len(args) == 1:
            values = self.require_vector(a)
            if not values and name in ('min', 'max'):
                raise _fc_Error('Il vettore è vuoto.')
            result = 0.0
            for i, item in enumerate(values):
                n = self.number(item)
                result = min(result, n) if name == 'min' and i else max(result, n) if name == 'max' and i else n if name in ('min', 'max') else self.add(result, n)
            return result
        n = self.number(a)
        if name == 'abs':
            return abs(n)
        if name == 'floor':
            return float(_fc_math.floor(n))
        if name == 'int':
            return float(_fc_math.trunc(n))
        if name == 'sgn':
            return float((n > 0) - (n < 0))
        if name == 'round':
            return self.round(n, args[1] if len(args) == 2 else 0.0)
        if name in ('min', 'max'):
            values = [self.number(v) for v in args]
            return min(values) if name == 'min' else max(values)
        if name == 'random':
            return float(_fc_random.randrange(self.integer(n, 1, 2147483647, 'Il limite')))
        if name == 'tochar':
            code = self.integer(n, 0, 0x10ffff, 'Il codice Unicode')
            if 0xd800 <= code <= 0xdfff:
                raise _fc_Error('Il codice Unicode è incompleto.')
            return chr(code)
        if name == 'tostring':
            return self.format(n)
        if name == 'tofixed':
            digits = self.integer(args[1], 0, 15, 'Le cifre decimali')
            rounded = self.round(n, digits)
            with _fc_decimal.localcontext() as context:
                context.prec = 400
                value = _fc_decimal.Decimal.from_float(rounded).quantize(_fc_decimal.Decimal(1).scaleb(-digits), rounding=_fc_decimal.ROUND_HALF_UP)
                if value == 0:
                    value = abs(value)
                return format(value, 'f')
        functions = {'sqrt': _fc_math.sqrt, 'sin': _fc_math.sin, 'cos': _fc_math.cos, 'tan': _fc_math.tan,
                     'arcsin': _fc_math.asin, 'arccos': _fc_math.acos, 'arctan': _fc_math.atan,
                     'ln': _fc_math.log, 'log': _fc_math.log, 'log10': _fc_math.log10}
        try:
            return self.number(functions[name](n))
        except (ValueError, OverflowError, KeyError):
            raise _fc_Error('Valore fuori dal dominio di ' + name + '.') from None

    def characters(self, value):
        text = self.text(value)
        try:
            return text.encode('utf-16-le', 'surrogatepass').decode('utf-16-le', 'strict')
        except UnicodeError:
            raise _fc_Error('Il testo contiene un carattere Unicode incompleto.') from None

    def require_closed(self, reading):
        occupied = self.read_text is not None if reading else self.write_name is not None
        if occupied:
            raise _fc_Error('Il canale è già aperto. Chiudilo prima di aprirne un altro.')

    def require_write(self):
        if self.write_name is None:
            raise _fc_Error('Apri prima un file in scrittura.')

    def file_name(self, name):
        if not isinstance(name, str) or not name.strip() or self.units(name) > 256 or any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in name):
            raise _fc_Error('Il nome del file deve contenere da 1 a 256 caratteri senza controlli.')
        self.validate_file_text(name)
        return name

    def validate_file_text(self, text):
        if not isinstance(text, str) or self.units(text) > 2097152:
            raise _fc_Error('Il file supera 2 milioni di caratteri.')
        try:
            data = text.encode('utf-8', 'strict')
        except UnicodeError:
            raise _fc_Error('Il file contiene caratteri Unicode non validi.') from None
        if len(data) > 4194304:
            raise _fc_Error('Il file supera 4 MiB.')
        return data

    def open_file(self, reading, name):
        self.require_closed(reading)
        name = self.file_name(name)
        if reading:
            text = self.file_reader(name)
            self.validate_file_text(text)
            self.read_name, self.read_text, self.read_position = name, text, 0
        else:
            self.write_name, self.write_parts = name, []
            self.write_size = self.write_bytes = 0

    def eof(self):
        if self.read_text is None:
            raise _fc_Error('Apri prima un file in lettura.')
        return self.read_position >= len(self.read_text)

    def read_file(self, name, declaration=None, parameter=None, vector=None, index_expression=None):
        index = None
        if index_expression is not None:
            self.require_vector(vector)
            position = index_expression()
            index = self.index(self.lookup(name) if self.references else vector, position)
        if self.eof():
            raise _fc_Error('Il file è terminato. Controlla EOF() prima di leggere.')
        end = self.read_position
        while end < len(self.read_text) and self.read_text[end] not in '\r\n':
            end += 1
        line = self.read_text[self.read_position:end]
        if self.units(line) > 32768:
            raise _fc_Error('Una riga del file supera 32768 caratteri.')
        value = self.parse_input(line, self.wants_text(declaration, parameter, index is not None))
        if index is not None:
            value = self.set_after(value, self.lookup(name) if self.references else vector, lambda: float(index))
        value = self.convert(value, declaration, parameter)
        self.accept_input(name, value, index is not None)
        if end < len(self.read_text) and self.read_text[end] == '\r':
            end += 1
            if end < len(self.read_text) and self.read_text[end] == '\n':
                end += 1
        elif end < len(self.read_text) and self.read_text[end] == '\n':
            end += 1
        self.read_position = end
        return value

    def write_file(self, value, newline=True):
        self.require_write()
        text = self.format(value) + ('\n' if newline else '')
        data = self.validate_file_text(text)
        if self.write_size + self.units(text) > 2097152 or self.write_bytes + len(data) > 4194304:
            raise _fc_Error('Il file generato supera il limite di dimensione.')
        self.write_parts.append(text)
        self.write_size += self.units(text)
        self.write_bytes += len(data)

    def close_file(self, reading):
        if reading:
            self.eof()
            self.read_name = self.read_text = None
            self.read_position = 0
        else:
            self.require_write()
            self.file_saver(self.write_name, ''.join(self.write_parts))
            self.write_name, self.write_parts = None, []
            self.write_size = self.write_bytes = 0

    def finish_files(self):
        if self.write_name is not None:
            self.close_file(False)
        if self.read_text is not None:
            self.close_file(True)

    def _read_path(self, name):
        with open(name, 'rb') as stream:
            data = stream.read(4194305)
        if len(data) > 4194304:
            raise _fc_Error('Il file supera 4 MiB.')
        encoding = 'utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig'
        try:
            return data.decode(encoding, 'strict')
        except UnicodeError:
            raise _fc_Error('Il file non è un testo UTF-8 o UTF-16 valido.') from None

    def _save_path(self, name, text):
        destination = _fc_pathlib.Path(name)
        overwrite = destination.exists()
        data = self.validate_file_text(text)
        try:
            draft = _fc_tempfile.NamedTemporaryFile(prefix='frociart-', suffix='.txt', dir=str(destination.parent), delete=False)
        except OSError:
            draft = _fc_tempfile.NamedTemporaryFile(prefix='frociart-', suffix='.txt', delete=False)
        with draft as stream:
            self.recovery_path = stream.name
            stream.write(data)
            stream.flush()
            _fc_os.fsync(stream.fileno())
        if overwrite:
            try:
                accepted = self.reader('Il file esiste. Scrivi SOVRASCRIVI per sostituirlo: ').strip() == 'SOVRASCRIVI'
            except EOFError:
                accepted = False
            if not accepted:
                raise _fc_Error('Salvataggio annullato. Il file originale è intatto; il testo generato è nella bozza recuperabile.')
        if overwrite:
            _fc_os.replace(self.recovery_path, destination)
        elif _fc_os.name == 'nt':
            # Windows rename is exclusive and works without hard-link permissions.
            _fc_os.rename(self.recovery_path, destination)
        else:
            # POSIX rename would overwrite a racing file; link is exclusive.
            _fc_os.link(self.recovery_path, destination)
            _fc_os.unlink(self.recovery_path)
        self.recovery_path = None

    def turtle_forward(self, distance, draw=True):
        distance = self.number(distance)
        radians = self.heading * _fc_math.pi / 180
        cosine = 0.0 if self.heading in (90, 270) else 1.0 if self.heading == 0 else -1.0 if self.heading == 180 else _fc_math.cos(radians)
        sine = 0.0 if self.heading in (0, 180) else 1.0 if self.heading == 90 else -1.0 if self.heading == 270 else _fc_math.sin(radians)
        x, y = self.x + distance * cosine, self.y + distance * sine
        if not _fc_math.isfinite(x) or not _fc_math.isfinite(y) or abs(x) > 1000000 or abs(y) > 1000000:
            raise _fc_Error('Il disegno supera le coordinate da -1000000 a 1000000.')
        if draw and (x != self.x or y != self.y):
            if len(self.segments) >= 10000:
                raise _fc_Error('Il disegno supera 10000 tratti.')
            self.segments.append((self.x, self.y, x, y, self.color))
        self.x, self.y, self.turtle_used = x, y, True

    def turtle_turn(self, angle, left=False):
        angle = _fc_math.fmod(self.number(angle), 360)
        self.heading = ((self.heading + (angle if left else -angle)) % 360 + 360) % 360
        self.turtle_used = True

    def turtle_home(self):
        self.x = self.y = 0.0
        self.heading, self.turtle_used = 90.0, True

    def turtle_clear(self):
        self.segments.clear()
        self.turtle_home()

    def turtle_pen(self, color):
        color = self.text(color).strip().lower()
        names = [('nero', 'black', '#000000'), ('bianco', 'white', '#FFFFFF'), ('grigio', 'gray', '#808080'),
                 ('rosso', 'red', '#FF0000'), ('arancione', 'orange', '#FF8C00'), ('giallo', 'yellow', '#FFFF00'),
                 ('verde', 'green', '#008000'), ('lime', 'lime', '#00FF00'), ('turchese', 'teal', '#008080'),
                 ('ciano', 'cyan', '#00FFFF'), ('blu', 'blue', '#0000FF'), ('azzurro', 'skyblue', '#00BFFF'),
                 ('viola', 'purple', '#800080'), ('magenta', 'magenta', '#FF00FF'), ('rosa', 'pink', '#FF69B4'),
                 ('marrone', 'brown', '#A52A2A'), ('grey', 'grey', '#808080')]
        for italian, english, code in names:
            if color in (italian, english):
                color = code
                break
        if _fc_re.fullmatch(r'#[0-9a-fA-F]{3}', color):
            color = '#' + ''.join(c * 2 for c in color[1:])
        if not _fc_re.fullmatch(r'#[0-9a-fA-F]{6}', color):
            raise _fc_Error('Colore non riconosciuto. Usa un nome o #RRGGBB.')
        self.color, self.turtle_used = color.upper(), True

    def show_drawing(self):
        if not self.turtle_used:
            return
        try:
            import turtle
            screen = turtle.Screen()
            screen.title('Frociart — disegno Python')
            screen.setup(900, 700)
            screen.tracer(0)
            pen = turtle.Turtle(visible=False)
            pen.speed(0)
            pen.pensize(2)
            points = [(0.0, 0.0), (self.x, self.y)]
            for x1, y1, x2, y2, color in self.segments:
                points.extend(((x1, y1), (x2, y2)))
            min_x, max_x = min(p[0] for p in points), max(p[0] for p in points)
            min_y, max_y = min(p[1] for p in points), max(p[1] for p in points)
            width, height = max(100, max_x - min_x), max(100, max_y - min_y)
            width = max(width, height * 9 / 7)
            height = max(height, width * 7 / 9)
            cx, cy = (min_x + max_x) / 2, (min_y + max_y) / 2
            screen.setworldcoordinates(cx - width * .6, cy - height * .6, cx + width * .6, cy + height * .6)
            for x1, y1, x2, y2, color in self.segments:
                pen.penup(); pen.goto(x1, y1); pen.pencolor(color); pen.pendown(); pen.goto(x2, y2)
            pen.penup(); pen.goto(self.x, self.y); pen.setheading(self.heading); pen.pencolor(self.color); pen.shape('turtle'); pen.showturtle()
            screen.update()
            screen.mainloop()
        except Exception as error:
            raise _fc_Error('La grafica Python richiede tkinter/Tk: ' + str(error)) from None

    def run(self, main, show_drawing=True):
        try:
            main()
            if show_drawing:
                self.show_drawing()
            return True
        except (_fc_Error, OSError, UnicodeError, RecursionError) as error:
            print('Frociart: ' + str(error), file=_fc_sys.stderr)
            if self.recovery_path:
                print('Bozza recuperabile: ' + self.recovery_path, file=_fc_sys.stderr)
            return False
