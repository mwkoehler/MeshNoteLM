/*
================================================================================
LLM Chat Message Model
Represents a single message in an LLM conversation with full metadata
================================================================================
*/

#nullable enable

using System;

namespace MeshNoteLM.Models;

public class LLMChatMessage
{
    /// <summary>
    /// Message sender role (user or assistant)
    /// </summary>
    public string Role { get; set; } = "user"; // "user" or "assistant"

    /// <summary>
    /// The actual message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Which LLM provider was used (e.g., "Claude", "OpenAI", "Gemini")
    /// Null for user messages
    /// </summary>
    public string? LLMProvider { get; set; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Optional error message if the LLM request failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Was this message sent successfully?
    /// </summary>
    public bool IsError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Format message for markdown display
    /// </summary>
    public string ToMarkdown()
    {
        var timestamp = Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        if (Role == "user")
        {
            return $"## {timestamp}\n**User:** {Content}\n";
        }
        else if (IsError)
        {
            return $"## {timestamp}\n**{LLMProvider} (Error):** {ErrorMessage}\n";
        }
        else
        {
            return $"## {timestamp}\n**{LLMProvider}:** {Content}\n";
        }
    }

    /// <summary>
    /// Parse a markdown message block back into LLMChatMessage
    /// </summary>
    public static LLMChatMessage? FromMarkdown(string markdownBlock)
    {
        try
        {
            var lines = markdownBlock.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;

            // Parse timestamp from ## header
            var timestampLine = lines[0].TrimStart('#', ' ');
            if (!DateTime.TryParse(timestampLine, out var timestamp))
                timestamp = DateTime.Now;

            // Parse role and content from **Role:** content
            var contentLine = lines[1];
            var message = new LLMChatMessage { Timestamp = timestamp };

            if (contentLine.StartsWith("**User:**"))
            {
                message.Role = "user";
                message.Content = contentLine["**User:**".Length..].Trim();
            }
            else if (contentLine.Contains("**") && contentLine.Contains(":**"))
            {
                message.Role = "assistant";
                var providerEnd = contentLine.IndexOf(":**");
                var providerStart = contentLine.IndexOf("**") + 2;
                message.LLMProvider = contentLine[providerStart..providerEnd].Trim();

                if (message.LLMProvider.EndsWith(" (Error)"))
                {
                    message.LLMProvider = message.LLMProvider.Replace(" (Error)", "");
                    message.ErrorMessage = contentLine[(providerEnd + 3)..].Trim();
                }
                else
                {
                    message.Content = contentLine[(providerEnd + 3)..].Trim();
                }
            }

            // Collect remaining lines as part of content
            if (lines.Length > 2)
            {
                var remainingContent = string.Join("\n", lines[2..]);
                message.Content += "\n" + remainingContent;
            }

            return message;
        }
        catch
        {
            return null;
        }
    }
}
