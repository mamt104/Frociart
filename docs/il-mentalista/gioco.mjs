const turn = document.querySelector('#turn');
const prompt = document.querySelector('#prompt');
const guess = document.querySelector('#guess');
const interval = document.querySelector('#interval');
const progress = document.querySelector('#progress');
const answers = document.querySelector('#answers');
const restart = document.querySelector('#restart');

let lower = 1;
let upper = 1000;
let questions = 1;

function middle() {
  return Math.floor((lower + upper) / 2);
}

function showQuestion() {
  turn.textContent = 'Domanda ' + questions + ' di 10';
  prompt.textContent = 'Il tuo numero è…';
  guess.textContent = String(middle());
  interval.textContent = lower === upper
    ? 'Resta un solo numero possibile'
    : 'Numeri possibili da ' + lower + ' a ' + upper;
  progress.value = questions;
}

function finish(found, number) {
  turn.textContent = 'Partita conclusa';
  prompt.textContent = found ? 'Ho indovinato!' : 'Le risposte si contraddicono.';
  guess.textContent = found ? String(number) : '∅';
  interval.textContent = found
    ? 'Ci sono riuscito in ' + questions + (questions === 1 ? ' domanda.' : ' domande.')
    : 'Hai escluso tutti i numeri da 1 a 1000.';
  answers.hidden = true;
  restart.hidden = false;
  restart.focus();
}

answers.addEventListener('click', event => {
  const button = event.target.closest('button[data-answer]');
  if (!button || answers.hidden) return;
  const number = middle();
  if (button.dataset.answer === 'equal') {
    finish(true, number);
    return;
  }
  if (button.dataset.answer === 'lower') upper = number - 1;
  if (button.dataset.answer === 'higher') lower = number + 1;
  if (lower > upper) {
    finish(false, number);
    return;
  }
  questions++;
  showQuestion();
});

restart.addEventListener('click', () => {
  lower = 1;
  upper = 1000;
  questions = 1;
  showQuestion();
  restart.hidden = true;
  answers.hidden = false;
  answers.querySelector('button').focus();
});

answers.hidden = false;
