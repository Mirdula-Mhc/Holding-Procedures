// HoldingAudioController.cs
//
// Mirrors AudioController.cs: reacts to OnStepLoaded, plays that step's voiceover (if any),
// and calls scenarioManager.NotifyVoiceoverComplete() when done - which is what
// HoldingUIController.HandleVoiceoverComplete() listens for to unlock Next on
// Familiarization/Recap steps.

using System.Collections;
using UnityEngine;

public class HoldingAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HoldingScenarioManager scenarioManager;

    [Header("Audio Source")]
    [SerializeField] private AudioSource voiceoverSource;

    private Coroutine voRoutine;

    private void OnEnable()
    {
        scenarioManager.OnStepLoaded += HandleStepLoaded;
    }

    private void OnDisable()
    {
        scenarioManager.OnStepLoaded -= HandleStepLoaded;
    }

    private void HandleStepLoaded(HoldingStepData step, int index)
    {
        if (voRoutine != null)
        {
            StopCoroutine(voRoutine);
            voRoutine = null;
        }

        if (voiceoverSource != null && voiceoverSource.isPlaying)
            voiceoverSource.Stop();

        // Simulation and SectorSelect steps gate Next on their own logic (checkpoint/selection
        // completion), not voiceover - so only play + wait for voiceover on the step types that
        // actually need it to gate navigation.
        if (step.stepType != HoldingStepType.Familiarization && step.stepType != HoldingStepType.Recap)
            return;

        voRoutine = StartCoroutine(PlayVoiceoverRoutine(step, index));
    }

    private IEnumerator PlayVoiceoverRoutine(HoldingStepData step, int index)
    {
        if (voiceoverSource != null && step.voiceover != null)
        {
            voiceoverSource.clip = step.voiceover;
            voiceoverSource.Play();

            yield return new WaitForSeconds(step.voiceover.length);
        }
        else
        {
            // No clip assigned yet - don't block testing, just give a short beat.
            yield return new WaitForSeconds(1.5f);
        }

        voRoutine = null;
        scenarioManager.NotifyVoiceoverComplete();
    }
}