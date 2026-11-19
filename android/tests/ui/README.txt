Native UI instrumentation for Frociart 1.2.0

UiProbe.java runs against the installed it.frociart.app MainActivity on Android 35.
It uses the actual native views, integrated Prova panel, sheets, flow engine,
touchscreen event injection, Android keyboard and UiAutomation screenshots.
This is a separate test APK: do not include instrumentation in the release APK.

Build with API 35 and sign the application and test APK with the same signing
identity. No private key or signing password is included in this source package.
Stage AndroidManifest.xml and UiProbe.java in a minimal project as follows:

  <stage>/app/src/main/AndroidManifest.xml
  <stage>/app/src/main/java/it/frociart/uichecks/UiProbe.java

Then use build_apk.py from the project root:

  python3 build_apk.py <stage> --sdk <SDK> --output <UiProbe.apk> \
    --build-dir <temporary-build> --signing-dir <existing-signing-directory>

Run the complete portrait and narrow-phone matrix on the existing Frociart AVD:

  python3 tests/ui/run_ui_checks.py --sdk <SDK> --apk <Frociart.apk> \
    --test-apk <UiProbe.apk> --results <results-directory> \
    --android-user-home <existing-android-user-home> --serial emulator-5554

The runner checks the original public certificate, package identity, alignment,
and APK hashes before installing with --no-streaming -r. It does not uninstall,
clear data, read signing secrets, start another AVD, or clear device-wide logs.
It restores display overrides, font scaling and the keyboard setting afterward.
It stops only the completed it.frociart.canvaschecks test package before display
changes, so its synthetic canvas checks cannot restart during this UI matrix.
The instrumentation restores the starting project, app and learning preferences,
theme and drafts, and deletes only projects created by the test. It does not
keep test XP or alter the user's original diagram. Use a dedicated test AVD.

Matrix: 432x960 px at 180 dpi (384 dp, font scale 1.0), then 360x720 px at
180 dpi (320 dp, font scale 1.3). Both cases include portrait and landscape.
Individual cases can be selected with --scenario portrait or --scenario narrow.

The default --phase full checks include:
- Header export, main actions and both gesture modes.
- Actual native tap selection, overlay actions, identical canvas bounds before
  and after selection and after clearing it.
- All six block choices visible in the picker without scrolling.
- Integrated Prova opens paused, keeps the diagram visible, enables read-only
  canvas navigation, and restores editing when closed.
- Execution reaches completion and shows result 5 without scrolling.
- Light and dark themes; typed Input preserved during theme changes/rotation.
- Native LIGHT_STATUS_BARS appearance follows each chosen theme.
- Input reached with Un passo, real keyboard and send control in both layouts,
  and no automatic continuation after submitting that manually reached Input.
- Runtime ERROR can be reopened and reset with enabled execution controls.
- Decision draft/primary footer with keyboard and rotation; custom branch
  labels (including &) and colors survive Apply and appear in branch choices.
- Keyboard visibility includes the real TYPE_INPUT_METHOD window bounds,
  stable across several observations; Apply/Send must lie wholly above it.

Run the second phase with --phase study, using a separate results directory:
- Home Impara and laboratory entry, readable preferred font sizing.
- Explanation before quiz; wrong answer, specific feedback, Retry and correct
  answer; all three questions, +30 XP once, and a repeat without duplicate XP.
- Rotation and a real Activity.recreate() with the active answer/mistake saved.
- All 24 lesson actions; native swipe events reach the last lesson.
- Lesson practice opens its executable example in the real editor.
- Ordina changes only positions with one Undo snapshot; Undo/Redo recover them.
- A real block drag switches automatic layout to free placement.
- A real drag of + onto verified empty canvas opens the linked-block picker;
  choosing Output creates one node and the source edge.
- Un passo animates the actual executed edge; closing Prova cancels animation.
- Contextual Trucco opens its learning help.

The native drag probe crosses touch slop with its first MOVE. DOWN/MOVE events
are queued asynchronously and the final UP is synchronized: a slow software
renderer must not convert a series of tiny, blocking MOVE calls into a long
press. Node/empty-target hit tests and overlay bounds are preconditions for
the native gesture. It still uses Android input injection, not direct gesture
method calls. The separate tests/motion harness covers the detailed gesture
state machine, historical 118 checks and system animation preference changes.

Each screenshot also checks that the active window belongs to Frociart or the
AOSP keyboard; a system error dialog makes the run fail as an environment issue.
Every failed assertion increments the error count. A Java exception, absent
native keyboard, missing completion marker, or nonzero error count fails the
runner. Success requires: UI_UPDATE_FINISHED <checks> errors=0.
Screenshots, native view bounds and report.txt are written under the tested
app's external-files directory uiqa-1.2.0[-study]/<scenario>, then pulled by the
runner. Finite surface transitions settle before capture; a renderer warning or
old transition frame cannot be used as evidence of final screen appearance.

Historical 1.0.0 screenshots/results remain under android-ui-final; they are
not evidence for this 1.2.0 release. Updated release results have their own
directory and hashes. These are functional/layout checks on a software Android
emulator; they are not measurements of physical-phone GPU performance.

Final 1.2.0 evidence is stored in tests/results/android-ui-1.2.0.
Its provenance file identifies the actual application/instrumentation and
source hashes for each accepted stage. Earlier 1.1.0 evidence remains unchanged
under tests/results/android-ui-1.1.0; it is not a substitute for this matrix.

The optional focused regression can be run with --scenario both --phase layout.
This phase checks completion/output 5, the real editor keyboard with stable
TYPE_INPUT_METHOD window bounds, Apply above that window, the landscape sheet
width excluding navigation insets, and preservation on return to portrait.
--phase editor-ime runs only the focused editor keyboard capture. The default
--phase full retains the editor/run regression workflow described above.
