using UnityEngine;
using System.Collections.Generic;
using LvlUp;
using LvlUp.Models;

/// <summary>
/// Example showing how to track player journey with checkpoints
/// This is useful for funnel analysis and understanding player progression
/// </summary>
public class PlayerJourneyExample : MonoBehaviour
{
    [Header("Checkpoint IDs (will be populated at runtime)")]
    private string tutorialStartCheckpointId;
    private string tutorialCompleteCheckpointId;
    private string firstLevelCheckpointId;
    private string firstPurchaseCheckpointId;

    private void Start()
    {
        // Setup checkpoints when game starts (typically do this once during game setup)
        SetupGameCheckpoints();
    }

    /// <summary>
    /// Create all checkpoints for your game
    /// You typically do this once during game initialization or from editor
    /// </summary>
    private void SetupGameCheckpoints()
    {
        // Create tutorial start checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "Tutorial Started",
            description: "Player has started the tutorial",
            type: "tutorial",
            order: 1,
            tags: new[] { "onboarding", "tutorial" },
            callback: response =>
            {
                if (response.success)
                {
                    tutorialStartCheckpointId = response.data.id;
                    Debug.Log($"✅ Tutorial Start checkpoint created: {tutorialStartCheckpointId}");
                }
            }
        );

        // Create tutorial complete checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "Tutorial Completed",
            description: "Player has completed the tutorial",
            type: "tutorial",
            order: 2,
            tags: new[] { "onboarding", "tutorial" },
            callback: response =>
            {
                if (response.success)
                {
                    tutorialCompleteCheckpointId = response.data.id;
                    Debug.Log($"✅ Tutorial Complete checkpoint created: {tutorialCompleteCheckpointId}");
                }
            }
        );

        // Create first level complete checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "First Level Complete",
            description: "Player completed their first level",
            type: "progression",
            order: 3,
            tags: new[] { "level", "milestone" },
            callback: response =>
            {
                if (response.success)
                {
                    firstLevelCheckpointId = response.data.id;
                    Debug.Log($"✅ First Level checkpoint created: {firstLevelCheckpointId}");
                }
            }
        );

        // Create first purchase checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "First Purchase",
            description: "Player made their first in-app purchase",
            type: "monetization",
            order: 10,
            tags: new[] { "iap", "milestone", "monetization" },
            callback: response =>
            {
                if (response.success)
                {
                    firstPurchaseCheckpointId = response.data.id;
                    Debug.Log($"✅ First Purchase checkpoint created: {firstPurchaseCheckpointId}");
                }
            }
        );
    }

    #region Checkpoint Recording Examples

    /// <summary>
    /// Call this when tutorial starts
    /// </summary>
    public void OnTutorialStart()
    {
        if (string.IsNullOrEmpty(tutorialStartCheckpointId))
        {
            Debug.LogWarning("Tutorial start checkpoint not created yet");
            return;
        }

        var metadata = new Dictionary<string, object>
        {
            { "startTime", System.DateTime.UtcNow.ToString("o") },
            { "platform", Application.platform.ToString() }
        };

        LvlUpManager.Instance.RecordCheckpoint(tutorialStartCheckpointId, metadata, response =>
        {
            if (response.success)
            {
                Debug.Log("✅ Tutorial start checkpoint recorded!");
            }
        });
    }

    /// <summary>
    /// Call this when tutorial completes
    /// </summary>
    public void OnTutorialComplete(float completionTimeSeconds)
    {
        if (string.IsNullOrEmpty(tutorialCompleteCheckpointId))
        {
            Debug.LogWarning("Tutorial complete checkpoint not created yet");
            return;
        }

        var metadata = new Dictionary<string, object>
        {
            { "completionTime", completionTimeSeconds },
            { "skipped", false },
            { "endTime", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.RecordCheckpoint(tutorialCompleteCheckpointId, metadata, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ Tutorial complete checkpoint recorded! Time: {completionTimeSeconds}s");
            }
        });
    }

    /// <summary>
    /// Call this when player completes first level
    /// </summary>
    public void OnFirstLevelComplete(int score, int stars)
    {
        if (string.IsNullOrEmpty(firstLevelCheckpointId))
        {
            Debug.LogWarning("First level checkpoint not created yet");
            return;
        }

        var metadata = new Dictionary<string, object>
        {
            { "score", score },
            { "stars", stars },
            { "completionTime", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.RecordCheckpoint(firstLevelCheckpointId, metadata, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ First level checkpoint recorded! Score: {score}, Stars: {stars}");
            }
        });
    }

    /// <summary>
    /// Call this when player makes first purchase
    /// </summary>
    public void OnFirstPurchase(string productId, float price, string currency)
    {
        if (string.IsNullOrEmpty(firstPurchaseCheckpointId))
        {
            Debug.LogWarning("First purchase checkpoint not created yet");
            return;
        }

        var metadata = new Dictionary<string, object>
        {
            { "productId", productId },
            { "price", price },
            { "currency", currency },
            { "purchaseTime", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.RecordCheckpoint(firstPurchaseCheckpointId, metadata, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ First purchase checkpoint recorded! Product: {productId}");
            }
        });
    }

    #endregion

    #region Journey Progress Tracking

    /// <summary>
    /// Get current player's journey progress
    /// </summary>
    public void CheckPlayerProgress()
    {
        LvlUpManager.Instance.GetPlayerJourneyProgress(response =>
        {
            if (response.success)
            {
                var progress = response.data;
                Debug.Log($"📊 Player Journey Progress:");
                Debug.Log($"   Completed: {progress.completedCheckpoints}/{progress.totalCheckpoints}");
                Debug.Log($"   Completion Rate: {progress.completionRate * 100}%");
                Debug.Log($"   Last Checkpoint: {progress.lastCheckpointDate}");

                // Display individual checkpoints
                foreach (var checkpoint in progress.checkpoints)
                {
                    string status = checkpoint.completed ? "✅" : "⏳";
                    Debug.Log($"   {status} {checkpoint.checkpointName} - {checkpoint.completedAt}");
                }

                // You could use this data to:
                // - Show progress UI to player
                // - Unlock rewards based on milestones
                // - Send personalized messages
                // - Adjust difficulty based on progress
            }
            else
            {
                Debug.LogError($"❌ Failed to get journey progress: {response.error}");
            }
        });
    }

    /// <summary>
    /// Example: Show progress UI based on journey data
    /// </summary>
    public void DisplayProgressUI()
    {
        LvlUpManager.Instance.GetPlayerJourneyProgress(response =>
        {
            if (response.success)
            {
                var progress = response.data;
                
                // Update your UI here
                // Example: progressBar.fillAmount = progress.completionRate;
                // Example: progressText.text = $"{progress.completedCheckpoints}/{progress.totalCheckpoints}";
                
                // Check for milestones
                if (progress.completionRate >= 0.5f)
                {
                    Debug.Log("🎉 Player has completed 50% of the journey!");
                    // Maybe show a reward or achievement
                }
            }
        });
    }

    #endregion

    #region Advanced Journey Tracking

    /// <summary>
    /// Example: Create dynamic level checkpoints
    /// </summary>
    public void CreateLevelCheckpoint(int levelNumber)
    {
        LvlUpManager.Instance.CreateCheckpoint(
            name: $"Level {levelNumber} Complete",
            description: $"Player completed level {levelNumber}",
            type: "level",
            order: 100 + levelNumber,
            tags: new[] { "level", $"level_{levelNumber}" },
            callback: response =>
            {
                if (response.success)
                {
                    Debug.Log($"✅ Level {levelNumber} checkpoint created!");
                    // Store checkpoint ID for later use
                    PlayerPrefs.SetString($"checkpoint_level_{levelNumber}", response.data.id);
                }
            }
        );
    }

    /// <summary>
    /// Example: Track level completion with checkpoint
    /// </summary>
    public void TrackLevelCompletion(int levelNumber, int score, float timeSeconds)
    {
        string checkpointId = PlayerPrefs.GetString($"checkpoint_level_{levelNumber}", "");
        
        if (string.IsNullOrEmpty(checkpointId))
        {
            Debug.LogWarning($"Checkpoint for level {levelNumber} not found, creating it now...");
            CreateLevelCheckpoint(levelNumber);
            return;
        }

        var metadata = new Dictionary<string, object>
        {
            { "score", score },
            { "timeSeconds", timeSeconds },
            { "attempts", GetLevelAttempts(levelNumber) },
            { "completionTime", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.RecordCheckpoint(checkpointId, metadata, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ Level {levelNumber} completion tracked!");
            }
        });
    }

    private int GetLevelAttempts(int levelNumber)
    {
        return PlayerPrefs.GetInt($"level_{levelNumber}_attempts", 0);
    }

    #endregion
}

