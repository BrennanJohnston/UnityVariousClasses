using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using System;
using Unity.Services.Authentication;

public static class TankVivox {
    
    public static bool IsLoggedIn { get { return VivoxService.Instance.IsLoggedIn; } }
    public static VoiceTransmissionMode TransmissionMode { get; private set; } = VoiceTransmissionMode.PTT;

    public static Action<VivoxMessage> ChannelMessageReceived;
    public static Action<string> ChannelJoined;
    public static Action<string> ChannelLeft;

    public enum VoiceTransmissionMode {
        PTT,
        Toggle,
        VoiceActivated
    }

    private static bool _transmitVoice = false;
    private static bool TransmitVoice {
        get { return _transmitVoice; }
        set {
            if (value == _transmitVoice) return;
            _transmitVoice = value;
            if (_transmitVoice) {
                VivoxService.Instance.UnmuteInputDevice();
            } else {
                VivoxService.Instance.MuteInputDevice();
            }
        }
    }

    // =====================================================================================

    // PUBLIC MEMBERS ======================================================================

    // =====================================================================================

    /// <summary>
    /// Initialize the VivoxService.  Must call this before anything else will function.
    /// </summary>
    /// <returns></returns>
    public static async Task InitializeAsync() {

        VivoxConfigurationOptions options = new VivoxConfigurationOptions();
        // set options here
        await VivoxService.Instance.InitializeAsync(options);

        if (TransmissionMode != VoiceTransmissionMode.VoiceActivated)
            VivoxService.Instance.MuteInputDevice();

        SubscribeToVivoxEvents();
    }

    /// <summary>
    /// Login to the VivoxService.  Call this after InitializeAsync() but before calling anything else.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public static async Task LoginAsync(string username) {
        if (IsLoggedIn || string.IsNullOrWhiteSpace(username)) return;

        LoginOptions options = new LoginOptions();
        options.DisplayName = username;
        Debug.Log("logging in to Vivox");
        await VivoxService.Instance.LoginAsync(options);
        Debug.Log("logged in to vivox");
    }

    /// <summary>
    /// Join a channel with the given unique channel name. The uniqueChannelName used in this game is the LobbyCode for a particular Lobby.
    /// </summary>
    /// <param name="uniqueChannelName"></param>
    public static async Task JoinTextAndAudioChannel(string uniqueChannelName) {
        if (string.IsNullOrWhiteSpace(uniqueChannelName)) return;
        ChannelOptions options = new ChannelOptions();
        options.MakeActiveChannelUponJoining = true;
        Debug.Log($"Joining Vivox channel {uniqueChannelName}");
        await VivoxService.Instance.JoinGroupChannelAsync(uniqueChannelName, ChatCapability.TextAndAudio, options);
        Debug.Log($"Joined Vivox channel {uniqueChannelName}");

        // DEBUG MESSAGE SEND ===========================================================================================
        //await VivoxService.Instance.SendChannelTextMessageAsync(uniqueChannelName, $"I JOINED THE CHANNEL WITH PLAYERID {VivoxService.Instance.SignedInPlayerId}");
    }

    public static async Task SendTextChatMessage(string uniqueChannelName, string chatText) {
        if (string.IsNullOrWhiteSpace(uniqueChannelName) || string.IsNullOrWhiteSpace(chatText)) return;

        await VivoxService.Instance.SendChannelTextMessageAsync(uniqueChannelName, $"{chatText}");
    }

    /// <summary>
    /// Call when the button for voice transmission was just pressed. Behaviour changes based on TransmissionMode (SetVoiceTransmissionMode() to change).
    /// </summary>
    /// <param name="pressed"></param>
    public static void TransmissionButtonPressed() {
        switch (TransmissionMode) {
            case VoiceTransmissionMode.PTT:
                TransmitVoice = true;
                break;

            case VoiceTransmissionMode.Toggle:
                TransmitVoice = !TransmitVoice;
                break;
        }
    }

    public static void TransmissionButtonReleased() {
        if(TransmissionMode == VoiceTransmissionMode.PTT) {
            TransmitVoice = false;
        }
    }

    public static void SetVoiceTransmissionMode(VoiceTransmissionMode mode) {
        TransmissionMode = mode;
        if(TransmissionMode == VoiceTransmissionMode.VoiceActivated) {
            TransmitVoice = true;
        }
    }

    /// <summary>
    /// Leave a channel with specified <paramref name="uniqueChannelName"/>
    /// </summary>
    /// <param name="uniqueChannelName"></param>
    /// <returns></returns>
    public static async Task LeaveChannel(string uniqueChannelName) {
        if (string.IsNullOrWhiteSpace(uniqueChannelName)) return;
        if (!VivoxService.Instance.ActiveChannels.ContainsKey(uniqueChannelName)) return;
        Debug.Log($"Leaving Vivox channel {uniqueChannelName}");
        try {
            await VivoxService.Instance.LeaveChannelAsync(uniqueChannelName);
        } catch (InvalidOperationException e) {
            Debug.Log($"Failed to leave channel {uniqueChannelName}.  The channel may have already been left.");
        }
    }

    /// <summary>
    /// Leave all channels currently a member of.
    /// </summary>
    public static async Task LeaveAllChannels() {
        try {
            await VivoxService.Instance.LeaveAllChannelsAsync();
        } catch (Exception e) {
            Debug.Log("Failed to LeaveAllChannels().  May not be in any channels.");
        }

    }

    // =====================================================================================

    // =====================================================================================

    // =====================================================================================





    // =====================================================================================

    // PRIVATE MEMBERS =====================================================================

    // =====================================================================================

    private static void SubscribeToVivoxEvents() {
        VivoxService.Instance.ChannelJoined += OnChannelJoined;
        VivoxService.Instance.ChannelLeft += OnChannelLeft;
        VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
    }

    private static void UnsubscribeFromVivoxEvents() {
        VivoxService.Instance.ChannelJoined -= OnChannelJoined;
        VivoxService.Instance.ChannelLeft -= OnChannelLeft;
        VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;
    }

    // =====================================================================================

    // =====================================================================================

    // =====================================================================================





    // =====================================================================================

    // EVENT HANDLERS ======================================================================

    // =====================================================================================

    private static void OnChannelJoined(string uniqueChannelName) {
        Debug.Log($"Joined Vivox Channel {uniqueChannelName}");
        ChannelJoined?.Invoke(uniqueChannelName);
    }

    private static void OnChannelLeft(string uniqueChannelName) {
        Debug.Log($"Left Vivox Channel {uniqueChannelName}");
        ChannelLeft?.Invoke(uniqueChannelName);
    }

    private static void OnChannelMessageReceived(VivoxMessage message) {
        Debug.Log($"Vivox message received on channel {message.ChannelName} from user {message.SenderDisplayName}");
        Debug.Log($"Message Text: {message.MessageText}");
        ChannelMessageReceived?.Invoke(message);
    }

    // =====================================================================================

    // =====================================================================================

    // =====================================================================================
}