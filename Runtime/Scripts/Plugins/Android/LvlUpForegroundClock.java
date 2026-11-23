package com.lvlup.analytics;

import android.app.Activity;
import android.app.Application;
import android.os.Bundle;
import android.os.SystemClock;

/**
 * Time the app has spent in the foreground since start(), counted the way GameAnalytics counts
 * sessions: the app is in the background only when none of its activities is started. A
 * full-screen ad or purchase dialog is another activity of the same app, so it keeps counting,
 * although Unity's own activity (and OnApplicationPause) is paused behind it.
 */
public final class LvlUpForegroundClock implements Application.ActivityLifecycleCallbacks {
    private static LvlUpForegroundClock instance;

    private int startedActivities;
    private long accumulatedMs;
    private long foregroundSinceMs;

    private LvlUpForegroundClock() {
    }

    /** Call once, from the foreground, with the current activity. Later calls do nothing. */
    public static synchronized void start(Activity currentActivity) {
        if (instance != null || currentActivity == null) {
            return;
        }

        LvlUpForegroundClock clock = new LvlUpForegroundClock();
        clock.startedActivities = 1;
        clock.foregroundSinceMs = SystemClock.elapsedRealtime();
        currentActivity.getApplication().registerActivityLifecycleCallbacks(clock);
        instance = clock;
    }

    public static synchronized long foregroundMillis() {
        if (instance == null) {
            return 0;
        }

        long running = instance.startedActivities > 0
                ? SystemClock.elapsedRealtime() - instance.foregroundSinceMs
                : 0;
        return instance.accumulatedMs + Math.max(0, running);
    }

    @Override
    public void onActivityStarted(Activity activity) {
        synchronized (LvlUpForegroundClock.class) {
            if (startedActivities++ == 0) {
                foregroundSinceMs = SystemClock.elapsedRealtime();
            }
        }
    }

    @Override
    public void onActivityStopped(Activity activity) {
        synchronized (LvlUpForegroundClock.class) {
            if (startedActivities == 0) {
                return;
            }

            if (--startedActivities == 0) {
                accumulatedMs += Math.max(0, SystemClock.elapsedRealtime() - foregroundSinceMs);
            }
        }
    }

    @Override
    public void onActivityCreated(Activity activity, Bundle savedInstanceState) {
    }

    @Override
    public void onActivityResumed(Activity activity) {
    }

    @Override
    public void onActivityPaused(Activity activity) {
    }

    @Override
    public void onActivitySaveInstanceState(Activity activity, Bundle outState) {
    }

    @Override
    public void onActivityDestroyed(Activity activity) {
    }
}
