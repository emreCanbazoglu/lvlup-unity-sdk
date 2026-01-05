using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using LvlUp;
using LvlUp.Models;
using TMPro; // Optional: Use TextMeshPro if available

/// <summary>
/// Example showing AI chat integration for in-game assistant
/// This creates an in-game chatbot that can answer player questions
/// </summary>
public class AIIntegrationExample : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField chatInputField;
    public TMP_Text chatDisplayText;
    public Button sendButton;
    public ScrollRect chatScrollRect;

    [Header("Chat Settings")]
    public int maxChatHistory = 10;

    private List<string> chatHistory = new List<string>();

    private void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendChatMessage);
        }

        // Example: Get AI insights on game start
        GetInitialAIInsights();
    }

    #region AI Chat

    /// <summary>
    /// Send a chat message to AI assistant
    /// </summary>
    public void SendChatMessage()
    {
        if (chatInputField == null || string.IsNullOrEmpty(chatInputField.text))
            return;

        string userMessage = chatInputField.text;
        AddMessageToChat("You", userMessage);
        chatInputField.text = "";

        // Add game context to help AI provide better responses
        var context = new Dictionary<string, object>
        {
            { "currentLevel", GetCurrentLevel() },
            { "playerScore", GetPlayerScore() },
            { "sessionDuration", Time.time },
            { "platform", Application.platform.ToString() }
        };

        LvlUpManager.Instance.SendAIMessage(userMessage, context, response =>
        {
            if (response.success)
            {
                AddMessageToChat("AI Assistant", response.data.message);
                Debug.Log($"✅ AI Response received: {response.data.message}");
            }
            else
            {
                AddMessageToChat("System", "Sorry, I couldn't process your message. Please try again.");
                Debug.LogError($"❌ AI Chat failed: {response.error}");
            }
        });
    }

    /// <summary>
    /// Add a message to the chat display
    /// </summary>
    private void AddMessageToChat(string sender, string message)
    {
        string formattedMessage = $"<b>{sender}:</b> {message}\n";
        chatHistory.Add(formattedMessage);

        // Keep only recent messages
        if (chatHistory.Count > maxChatHistory)
        {
            chatHistory.RemoveAt(0);
        }

        // Update display
        if (chatDisplayText != null)
        {
            chatDisplayText.text = string.Join("\n", chatHistory);
        }

        // Scroll to bottom
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    #endregion

    #region AI Insights

    /// <summary>
    /// Get AI insights about game analytics
    /// </summary>
    public void GetInitialAIInsights()
    {
        var filters = new Dictionary<string, object>
        {
            { "timeframe", "7d" },
            { "focus", "retention" },
            { "includeRecommendations", true }
        };

        LvlUpManager.Instance.GetAIInsights(filters, response =>
        {
            if (response.success)
            {
                var insights = response.data;
                Debug.Log($"📊 AI Insights Summary: {insights.summary}");
                
                if (insights.insights != null)
                {
                    foreach (var insight in insights.insights)
                    {
                        Debug.Log($"   💡 {insight.title}: {insight.description} (Impact: {insight.impact})");
                    }
                }

                // You could display these insights in your game's dashboard
                DisplayInsightsInGame(insights);
            }
            else
            {
                Debug.LogError($"❌ Failed to get AI insights: {response.error}");
            }
        });
    }

    /// <summary>
    /// Display insights in game UI (customize based on your UI)
    /// </summary>
    private void DisplayInsightsInGame(AIInsightsResponse insights)
    {
        // Example: Show insights as a notification or in a dashboard
        // You would customize this based on your game's UI system
        
        Debug.Log("=== AI INSIGHTS ===");
        Debug.Log(insights.summary);
        
        if (insights.recommendations != null)
        {
            Debug.Log("\nRecommendations:");
            foreach (var rec in insights.recommendations)
            {
                Debug.Log($"  • {rec.Key}: {rec.Value}");
            }
        }
    }

    /// <summary>
    /// Example: Ask AI for help with specific game feature
    /// </summary>
    public void AskAIForHelp(string topic)
    {
        string message = $"Can you help me with {topic}?";
        
        var context = new Dictionary<string, object>
        {
            { "helpTopic", topic },
            { "playerLevel", GetCurrentLevel() },
            { "sessionTime", Time.time }
        };

        LvlUpManager.Instance.SendAIMessage(message, context, response =>
        {
            if (response.success)
            {
                // Show help message in a popup or tutorial
                ShowHelpPopup(response.data.message);
            }
        });
    }

    /// <summary>
    /// Example: Get personalized recommendations
    /// </summary>
    public void GetPersonalizedRecommendations()
    {
        var context = new Dictionary<string, object>
        {
            { "playerLevel", GetCurrentLevel() },
            { "totalPlayTime", GetTotalPlayTime() },
            { "lastPlayedLevel", GetLastPlayedLevel() },
            { "purchaseHistory", GetPurchaseHistory() }
        };

        string message = "What should I focus on next to improve my gameplay?";

        LvlUpManager.Instance.SendAIMessage(message, context, response =>
        {
            if (response.success)
            {
                Debug.Log($"🎯 AI Recommendation: {response.data.message}");
                // Display recommendation to player
                ShowRecommendation(response.data.message);
            }
        });
    }

    /// <summary>
    /// Example: Get difficulty adjustment suggestion from AI
    /// </summary>
    public void GetDifficultyRecommendation()
    {
        var context = new Dictionary<string, object>
        {
            { "currentDifficulty", GetCurrentDifficulty() },
            { "recentDeaths", GetRecentDeaths() },
            { "averageCompletionTime", GetAverageCompletionTime() },
            { "playerSkillLevel", CalculatePlayerSkillLevel() }
        };

        string message = "Should I adjust the game difficulty for this player?";

        LvlUpManager.Instance.SendAIMessage(message, context, response =>
        {
            if (response.success)
            {
                Debug.Log($"🎮 Difficulty Recommendation: {response.data.message}");
                // Use AI suggestion to adjust game difficulty
                ProcessDifficultyRecommendation(response.data.message);
            }
        });
    }

    #endregion

    #region Example Helper Methods

    private void ShowHelpPopup(string message)
    {
        // Implement your help popup UI here
        Debug.Log($"[HELP] {message}");
    }

    private void ShowRecommendation(string recommendation)
    {
        // Implement your recommendation UI here
        Debug.Log($"[RECOMMENDATION] {recommendation}");
    }

    private void ProcessDifficultyRecommendation(string recommendation)
    {
        // Parse AI recommendation and adjust difficulty
        if (recommendation.ToLower().Contains("increase"))
        {
            Debug.Log("Increasing difficulty...");
            // Increase game difficulty
        }
        else if (recommendation.ToLower().Contains("decrease"))
        {
            Debug.Log("Decreasing difficulty...");
            // Decrease game difficulty
        }
    }

    // Game state helpers (customize based on your game)
    private int GetCurrentLevel() => PlayerPrefs.GetInt("CurrentLevel", 1);
    private int GetPlayerScore() => PlayerPrefs.GetInt("PlayerScore", 0);
    private float GetTotalPlayTime() => PlayerPrefs.GetFloat("TotalPlayTime", 0f);
    private int GetLastPlayedLevel() => PlayerPrefs.GetInt("LastPlayedLevel", 1);
    private string GetCurrentDifficulty() => PlayerPrefs.GetString("Difficulty", "Normal");
    private int GetRecentDeaths() => PlayerPrefs.GetInt("RecentDeaths", 0);
    private float GetAverageCompletionTime() => PlayerPrefs.GetFloat("AvgCompletionTime", 60f);
    
    private float CalculatePlayerSkillLevel()
    {
        // Calculate based on performance metrics
        float deathRate = GetRecentDeaths() / Mathf.Max(1f, GetCurrentLevel());
        float timeEfficiency = 1f / Mathf.Max(1f, GetAverageCompletionTime());
        return (1f - deathRate) * timeEfficiency * 100f;
    }

    private List<string> GetPurchaseHistory()
    {
        // Return player's purchase history
        return new List<string> { "starter_pack", "coin_bundle_small" };
    }

    #endregion

    #region UI Event Handlers (Optional Examples)

    /// <summary>
    /// Example button handler: Ask about strategy
    /// </summary>
    public void OnAskStrategyButton()
    {
        AskAIForHelp("game strategy for my current level");
    }

    /// <summary>
    /// Example button handler: Ask about tips
    /// </summary>
    public void OnAskTipsButton()
    {
        AskAIForHelp("tips to improve my score");
    }

    /// <summary>
    /// Example button handler: Get insights
    /// </summary>
    public void OnShowInsightsButton()
    {
        GetInitialAIInsights();
    }

    /// <summary>
    /// Example button handler: Get recommendations
    /// </summary>
    public void OnGetRecommendationsButton()
    {
        GetPersonalizedRecommendations();
    }

    #endregion
}

