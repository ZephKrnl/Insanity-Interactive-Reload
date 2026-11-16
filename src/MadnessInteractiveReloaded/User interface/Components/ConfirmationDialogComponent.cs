using System;
using Walgelijk;

namespace MIR;

/// <summary>
/// A message box with a button for notifying the user and having them click a button to confirm they saw the message.
/// </summary>
public class ConfirmationDialogComponent : Component
{
    public string Title;
    public string Message;
    public Action OnAgree;

    public bool RemoveEntityOnClose = true;

    /// <summary>
    /// The Onion node of the dialog's window, set by <see cref="ConfirmationDialogSystem"/> each frame it draws it. Pad
    /// navigation stays inside it while the dialog is open.
    /// </summary>
    public int? WindowNode;

    /// <summary>
    /// The Onion node of the dialog's "No" button, set by <see cref="ConfirmationDialogSystem"/> each frame it draws it. Pad
    /// focus starts there when the dialog opens.
    /// </summary>
    public int? CancelNode;

    public ConfirmationDialogComponent(string title, string message, Action onAgree)
    {
        Title = title;
        Message = message;
        OnAgree = onAgree;
    }

    public ConfirmationDialogComponent(string message, Action onAgree)
    {
        Title = "Confirmation";
        Message = message;
        OnAgree = onAgree;
    }
}

