// HoldingSimulationController.cs
//
// Owns everything that happens during a Simulation step: flying the aircraft, pausing at
// checkpoints, the course knob, the Start Timer popup, per-leg speed, and all simulation audio.
// HoldingUIController only tells it "a simulation step started" / "stop".
//
// At a checkpoint the flow depends on its mode:
//   KnobThenTimer : pause -> pause audio -> knob -> correct panel -> post-knob audio -> timer -> fly leg
//   KnobOnly      : pause -> pause audio -> knob -> correct panel -> fly on at cruise speed
//   TimerOnly     : pause -> pause audio -> timer -> fly leg

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class HoldingSimulationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HoldingScenarioManager scenarioManager;
    [SerializeField] private SplineAircraftMover aircraftMover;
    [SerializeField] private HoldingHsiFeeder hsiFeeder;
    [SerializeField] private HoldingMapIconFollower mapIconFollower;
    [SerializeField] private HoldingMapView mapView;
    [SerializeField] private HoldingTimerPopup timerPopup;
    [SerializeField] private HoldingCourseKnob courseKnob;

    [Header("Audio")]
    [Tooltip("Plays pause audio and post-knob audio. The simulation waits for these to finish.")]
    [SerializeField] private AudioSource blockingAudioSource;

    [Tooltip("Plays parallel audio while the aircraft flies. Never blocks anything.")]
    [SerializeField] private AudioSource parallelAudioSource;

    private HoldingStepData activeStep;
    private int nextCheckpointIndex;
    private int nextParallelAudioIndex;
    private List<HoldingParallelAudio> sortedParallel = new List<HoldingParallelAudio>();

    private Coroutine flightRoutine;
    private Coroutine checkpointRoutine;

    // ==================================================
    // PUBLIC
    // ==================================================

    /// <summary>Starts (or, if alreadyCompleted, just parks) the simulation for a step.</summary>
    public void BeginStep(HoldingStepData step, bool alreadyCompleted)
    {
        StopEverything();

        activeStep = step;
        nextCheckpointIndex = 0;
        nextParallelAudioIndex = 0;

        SplineContainer worldSpline = ResolveSceneSpline(step.worldSpline);
        SplineContainer uiSpline = ResolveSceneSpline(step.uiSpline);

        if (hsiFeeder != null)
        {
            hsiFeeder.approachCourse = step.approachCourse;
            hsiFeeder.holdingInboundCourse = step.holdingInboundCourse;
            hsiFeeder.ResetFixCrossing();
        }

        if (aircraftMover != null && worldSpline != null)
        {
            aircraftMover.speed = step.defaultCruiseSpeed;
            aircraftMover.SetSpline(worldSpline);

            if (alreadyCompleted)
            {
                // Revisit: no flight, no knob, no timer, no audio. Park at the end.
                aircraftMover.Pause();
                aircraftMover.SeekToNormalized(1f);
            }
            else
            {
                aircraftMover.Play();
            }
        }

        if (mapIconFollower != null && uiSpline != null)
            mapIconFollower.SetUiSpline(uiSpline);

        if (mapView != null)
        {
            mapView.ClearEntryPath();
            if (step.showDottedEntryPath && uiSpline != null)
                mapView.ShowEntryPath(uiSpline, step.entryPathEndT);
        }

        if (alreadyCompleted)
            return;

        BuildParallelList(step);
        flightRoutine = StartCoroutine(FlightWatcher());
    }

    /// <summary>Stops everything: flight watching, knob, timer, audio. Call when leaving a step.</summary>
    public void StopEverything()
    {
        if (flightRoutine != null) { StopCoroutine(flightRoutine); flightRoutine = null; }
        if (checkpointRoutine != null) { StopCoroutine(checkpointRoutine); checkpointRoutine = null; }

        if (courseKnob != null) courseKnob.Hide();
        if (timerPopup != null) timerPopup.Hide();
        if (hsiFeeder != null)
        {
            hsiFeeder.courseOverride = false;
        }

        if (blockingAudioSource != null && blockingAudioSource.isPlaying) blockingAudioSource.Stop();
        if (parallelAudioSource != null && parallelAudioSource.isPlaying) parallelAudioSource.Stop();

        activeStep = null;
    }

    // ==================================================
    // FLIGHT WATCHER
    // Runs while the aircraft is flying. Fires parallel audio, detects checkpoints and the end.
    // ==================================================

    private IEnumerator FlightWatcher()
    {
        while (activeStep != null && aircraftMover != null)
        {
            float t = aircraftMover.NormalizedT;

            // Parallel audio: fires once when the aircraft passes each trigger T.
            while (nextParallelAudioIndex < sortedParallel.Count &&
                   t >= sortedParallel[nextParallelAudioIndex].triggerT)
            {
                PlayParallel(sortedParallel[nextParallelAudioIndex].clip);
                nextParallelAudioIndex++;
            }

            HoldingCheckpoint[] checkpoints = activeStep.checkpoints;
            int count = checkpoints != null ? checkpoints.Length : 0;

            if (nextCheckpointIndex < count)
            {
                if (t >= checkpoints[nextCheckpointIndex].pauseAtT)
                {
                    flightRoutine = null;
                    checkpointRoutine = StartCoroutine(RunCheckpoint(nextCheckpointIndex));
                    yield break;
                }
            }
            else if (aircraftMover.ReachedEnd)
            {
                flightRoutine = null;
                scenarioManager.NotifySimulationStepComplete();
                yield break;
            }

            yield return null;
        }
    }

    // ==================================================
    // CHECKPOINT FLOW
    // ==================================================

    private IEnumerator RunCheckpoint(int index)
    {
        HoldingCheckpoint cp = activeStep.checkpoints[index];

        aircraftMover.Pause();
        scenarioManager.NotifyCheckpointReached(index);

        // 1. Pause audio: everything waits until it finishes.
        yield return PlayBlocking(cp.pauseAudio);

        // 2. Knob (KnobThenTimer / KnobOnly).
        bool hasKnob = cp.mode != HoldingCheckpointMode.TimerOnly;
        if (hasKnob && courseKnob != null)
        {
            bool knobDone = false;
            float submittedCourse = 0f;

            if (hsiFeeder != null)
                hsiFeeder.courseOverride = true;   // freeze the whole HSI, knob drives only the course pointer

            courseKnob.Show(cp.requiredCourse, submitted =>
            {
                submittedCourse = submitted;
                knobDone = true;
            });

            while (!knobDone)
                yield return null;

            if (hsiFeeder != null)
            {
                hsiFeeder.HoldCourse(submittedCourse);  // keep the user's course on the pointer
                hsiFeeder.courseOverride = false;       // unfreeze the rest of the HSI
            }

            // 3. Post-knob audio (only when a timer follows).
            if (cp.mode == HoldingCheckpointMode.KnobThenTimer)
                yield return PlayBlocking(cp.postKnobAudio);
        }

        // 4. Timer (KnobThenTimer / TimerOnly), or straight back to flying (KnobOnly).
        if (cp.mode == HoldingCheckpointMode.KnobOnly)
        {
            aircraftMover.speed = activeStep.defaultCruiseSpeed;
        }
        else
        {
            bool timerStarted = false;
            bool timerFinished = false;

            if (timerPopup != null)
            {
                timerPopup.Show(
                    cp.timerMinutes,
                    cp.simulatedDuration,
                    () => timerStarted = true,
                    () => timerFinished = true);

                while (!timerStarted)
                    yield return null;
            }

            ApplyLegSpeed(cp);

            // Aircraft flies the timed leg while the countdown runs; the watcher resumes below,
            // so parallel audio and the next checkpoint keep working during the leg.
            aircraftMover.Play();
            nextCheckpointIndex = index + 1;
            checkpointRoutine = null;
            flightRoutine = StartCoroutine(FlightWatcherAfterLeg(() => timerFinished));
            yield break;
        }

        nextCheckpointIndex = index + 1;
        aircraftMover.Play();
        checkpointRoutine = null;
        flightRoutine = StartCoroutine(FlightWatcher());
    }

    // Same as FlightWatcher, but restores cruise speed once the countdown has finished.
    private IEnumerator FlightWatcherAfterLeg(System.Func<bool> timerFinished)
    {
        bool speedRestored = false;

        while (activeStep != null && aircraftMover != null)
        {
            if (!speedRestored && timerFinished())
            {
                aircraftMover.speed = activeStep.defaultCruiseSpeed;
                speedRestored = true;
            }

            float t = aircraftMover.NormalizedT;

            while (nextParallelAudioIndex < sortedParallel.Count &&
                   t >= sortedParallel[nextParallelAudioIndex].triggerT)
            {
                PlayParallel(sortedParallel[nextParallelAudioIndex].clip);
                nextParallelAudioIndex++;
            }

            HoldingCheckpoint[] checkpoints = activeStep.checkpoints;
            int count = checkpoints != null ? checkpoints.Length : 0;

            if (nextCheckpointIndex < count)
            {
                if (t >= checkpoints[nextCheckpointIndex].pauseAtT)
                {
                    flightRoutine = null;
                    checkpointRoutine = StartCoroutine(RunCheckpoint(nextCheckpointIndex));
                    yield break;
                }
            }
            else if (aircraftMover.ReachedEnd)
            {
                flightRoutine = null;
                scenarioManager.NotifySimulationStepComplete();
                yield break;
            }

            yield return null;
        }
    }

    private void ApplyLegSpeed(HoldingCheckpoint cp)
    {
        float targetEndT = cp.legEndT;
        float currentT = aircraftMover.NormalizedT;

        float deltaT = targetEndT - currentT;
        if (deltaT < 0f && currentT > 0.9f)
            deltaT += 1.0f;

        deltaT = Mathf.Max(0.01f, deltaT);

        SplineContainer spline = ResolveSceneSpline(activeStep.worldSpline);
        float totalLength = SplineUtility.CalculateLength(spline.Spline, spline.transform.localToWorldMatrix);
        float legDistance = deltaT * totalLength;

        // Auto-calculated: arrives at legEndT exactly as the countdown hits 00:00.
        aircraftMover.speed = legDistance / Mathf.Max(0.5f, cp.simulatedDuration);
    }

    // ==================================================
    // AUDIO
    // ==================================================

    // Plays a clip on the blocking source and waits for it to finish. No clip = no wait.
    private IEnumerator PlayBlocking(AudioClip clip)
    {
        if (clip == null || blockingAudioSource == null)
            yield break;

        blockingAudioSource.clip = clip;
        blockingAudioSource.Play();

        yield return new WaitForSeconds(clip.length);
    }

    private void PlayParallel(AudioClip clip)
    {
        if (clip == null || parallelAudioSource == null)
            return;

        // PlayOneShot so overlapping parallel clips don't cut each other off.
        parallelAudioSource.PlayOneShot(clip);
    }

    private void BuildParallelList(HoldingStepData step)
    {
        sortedParallel.Clear();

        if (step.parallelAudios == null)
            return;

        foreach (HoldingParallelAudio entry in step.parallelAudios)
        {
            if (entry.clip != null)
                sortedParallel.Add(entry);
        }

        sortedParallel.Sort((a, b) => a.triggerT.CompareTo(b.triggerT));
    }

    // ==================================================
    // SPLINE RESOLUTION
    // ==================================================

    private SplineContainer ResolveSceneSpline(SplineContainer prefabOrSceneSpline)
    {
        if (prefabOrSceneSpline == null) return null;

        if (prefabOrSceneSpline.gameObject.scene.IsValid())
            return prefabOrSceneSpline;

        GameObject sceneObj = GameObject.Find(prefabOrSceneSpline.name);
        if (sceneObj != null && sceneObj.TryGetComponent(out SplineContainer sceneSpline))
            return sceneSpline;

        return prefabOrSceneSpline;
    }
}