package com.lvlup.analytics;

import android.app.Activity;
import android.app.Application;
import android.os.Bundle;
import android.os.SystemClock;

import java.util.Collections;
import java.util.Set;
import java.util.WeakHashMap;

/**
 * Time the app has spent in the foreground since start(), counted the way GameAnalytics counts
 * sessions: the app is in the background only when none of its activities is started. A
 * full-screen ad or purchase dialog is another activity of the same app, so it keeps counting,
 * although Unity's own activity (and OnApplicationPause) is paused behind it.
 *
 * Started activities are tracked by identity, not counted: the activity passed to start() may still
 * receive its own onActivityStarted, and a count would then never return to zero in the background.
 */
public final class LvlUpForegroundClock implements Application.ActivityLifecycleCallbacks {
    private static LvlUpForegroundClock instance;

    private final Set<Activity> startedActivities = Collections.newSetFromMap(new WeakHashMap<Activity, Boolean>());
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
        clock.startedActivities.add(currentActivity);
        clock.foregroundSinceMs = SystemClock.elapsedRealtime();
        currentActivity.getApplication().registerActivityLifecycleCallbacks(clock);
        instance = clock;
    }

    public static synchronized long foregroundMillis() {
        if (instance == null) {
            return 0;
        }

        long running = !instance.startedActivities.isEmpty()
                ? SystemClock.elapsedRealtime() - instance.foregroundSinceMs
                : 0;
        return instance.accumulatedMs + Math.max(0, running);
    }

    @Override
    public void onActivityStarted(Activity activity) {
        synchronized (LvlUpForegroundClock.class) {
            if (startedActivities.isEmpty()) {
                foregroundSinceMs = SystemClock.elapsedRealtime();
            }
            startedActivities.add(activity);
        }
    }

    @Override
    public void onActivityStopped(Activity activity) {
        markStopped(activity);
    }

    private void markStopped(Activity activity) {
        synchronized (LvlUpForegroundClock.class) {
            if (!startedActivities.remove(activity)) {
                return;
            }

            if (startedActivities.isEmpty()) {
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
        markStopped(activity);
    }
}
