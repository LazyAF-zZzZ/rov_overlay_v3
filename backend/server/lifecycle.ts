// Shutting down cleanly when the desktop app goes away.
//
// In v3 this server is a child process of the desktop app, not code running inside
// Electron. The app stops it when it closes, and a plain kill loses whatever the
// operator changed in the last 150 ms (saveStateSoon debounces the write) and leaves
// the SQLite WAL un-checkpointed.
//
// The desktop app keeps our stdin open for as long as it runs and closes it to ask us
// to stop. We flush, close the database and exit. stdin also closes when the app
// crashes, so this path runs then too, unless Windows' job object gets there first.
//
// Only active when ROV_EXIT_WITH_PARENT=1, so `npm start` in a terminal behaves as
// it always has.

import { flushState } from './store/live-state';
import { closeDatabase } from './store/db';

export function shutdown(code = 0): void {
  try {
    flushState();
  } catch (error) {
    console.warn(`Could not save state on shutdown: ${(error as Error).message}`);
  }
  try {
    closeDatabase();
  } catch (error) {
    console.warn(`Could not close the database on shutdown: ${(error as Error).message}`);
  }
  process.exit(code);
}

export function exitWithParent(): void {
  let stopping = false;
  const stop = (): void => {
    if (stopping) return;
    stopping = true;
    console.log('Parent closed the pipe; saving and shutting down.');
    shutdown(0);
  };

  process.stdin.on('end', stop);
  process.stdin.on('close', stop);
  process.stdin.on('error', stop);
  process.stdin.resume();
}
