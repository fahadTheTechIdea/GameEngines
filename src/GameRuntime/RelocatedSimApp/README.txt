These files are the former Beep.SimEngine/App shell (SimApp, ClockControls/PlayScene, CoverageTracker).
They drove a game through the engine clock, Update() and Clock.Speed/Pause, which now belong to the host layer,
so they no longer compile and are not part of any build. Kept for whoever ports an app shell onto GameRuntime.
