using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class TankInputManager : MonoBehaviour {
    [SerializeField] private InputActionMap TankActionMap;
    [SerializeField] private InputActionMap MenuActionMap;
    [SerializeField] private InputActionMap ScoreboardActionMap;

    public static TankInputManager Singleton { get; private set; } = null;

    private const float MIN_GAMEPAD_LOOK_SENSITIVITY = 1.0f;
    private const float MAX_GAMEPAD_LOOK_SENSITIVITY = 10.0f;
    private const float GAMEPAD_VERTICAL_LOOK_MULTIPLIER = 0.5f;
    private const float VERTICAL_LOOK_DEADZONE = 0.5f;
    private float _gamepadLookSensitivity = 2.5f;

    public TankControlData TankControlInputs { get; } = new TankControlData();

    public float GamepadLookSensitivity {
        get { return _gamepadLookSensitivity; }
        set { _gamepadLookSensitivity = Mathf.Clamp(value, MIN_GAMEPAD_LOOK_SENSITIVITY, MAX_GAMEPAD_LOOK_SENSITIVITY); }
    }

    
    public static Action<InputControlMode, InputControlMode> ControlModeChanged;
    public static Action<bool> ShowScoreboardInputChanged;

    public InputControlMode CurrentControlMode { get; private set; } = InputControlMode.Menu;
    public System.Type CurrentDeviceType { get; private set; } = typeof(Keyboard);

    public enum InputControlMode {
        Tank,
        Menu,
        Scoreboard
    }

    public class TankControlData {
        public Vector2 LookDelta { get; internal set; }
        public float MoveDelta { get; internal set; }
        public float TurnDelta { get; internal set; }

        public Action FirePrimary;
        public Action FireSecondary;
        public Action NextWeapon;
        public Action PreviousWeapon;

        internal TankControlData() {
            LookDelta = default;
            MoveDelta = default;
            TurnDelta = default;
        }

        internal TankControlData(Vector2 _lookDelta, float _moveDelta, float _turnDelta) {
            LookDelta = _lookDelta;
            MoveDelta = _moveDelta;
            TurnDelta = _turnDelta;
        }

        internal void ResetControlData() {
            LookDelta = default;
            MoveDelta = default;
            TurnDelta = default;
        }
    }

    void Awake() {
        if(Singleton != null) {
            Debug.Log("TankInputManager.Singleton already exists, destroying duplicate.");
            Destroy(this);
            return;
        }

        Singleton = this;
    }

    void Start() {
        //TankActionMap.Enable();
        //MenuActionMap.Enable();
        SetControlMode(InputControlMode.Tank);
        
        //TankActionMap.actionTriggered += OnTankActionTriggered;

        // Set listeners for Tank Actions
        for(int i = 0; i < TankActionMap.actions.Count; i++) {
            InputAction action = TankActionMap.actions[i];
            switch (action.name) {
                case "Move":
                    action.performed += OnMovePerformed;
                    action.canceled += OnMoveCanceled;
                break;

                case "Turn":
                    action.performed += OnTurnPerformed;
                    action.canceled += OnTurnCanceled;
                break;

                case "Look":
                    action.started += OnLookStarted;
                    action.performed += OnLookPerformed;
                    action.canceled += OnLookCanceled;
                break;

                case "FirePrimary":
                    action.performed += OnFirePrimaryPerformed;
                break;

                case "FireSecondary":
                    action.performed += OnFireSecondaryPerformed;
                break;

                case "SwitchWeapon":
                    action.performed += OnSwitchWeaponPerformed;
                break;

                case "OpenScoreboard":
                    action.performed += OnOpenScoreboardPerformed;
                break;
            }
        }

        // Set listeners for MenuActionMap
        // TODO

        // Set listeners for ScoreboardActionMap
        for (int i = 0; i < ScoreboardActionMap.actions.Count; i++) {
            InputAction action = ScoreboardActionMap.actions[i];
            switch (action.name) {
                case "CloseScoreboard":
                    action.performed += OnCloseScoreboardCanceled;
                break;
            }
        }
    }

    void Update() {
        // Debug.Log(TankControlInputs.LookDelta + " : " + TankControlInputs.MoveDelta + " : " + TankControlInputs.TurnDelta);
    }

    public void SetControlMode(InputControlMode controlMode) {
        if (controlMode == CurrentControlMode) return;
        InputControlMode oldControlMode = CurrentControlMode;
        CurrentControlMode = controlMode;

        // Disable old control mode
        switch (oldControlMode) {
            case InputControlMode.Menu:
                EnableMenuControlMode(false);
            break;

            case InputControlMode.Tank:
                EnableTankControlMode(false);
            break;

            case InputControlMode.Scoreboard:
                EnableScoreboardControlMode(false);
            break;
        }

        // Enable new control mode
        switch (CurrentControlMode) {
            case InputControlMode.Menu:
                EnableMenuControlMode(true);
            break;

            case InputControlMode.Tank:
                EnableTankControlMode(true);
            break;

            case InputControlMode.Scoreboard:
                EnableScoreboardControlMode(true);
            break;
        }

        ControlModeChanged?.Invoke(oldControlMode, CurrentControlMode);
    }

    private void EnableTankControlMode(bool enable) {
        if (TankActionMap.enabled == enable) return;

        if (enable) {
            Cursor.lockState = CursorLockMode.Locked;
            TankActionMap.Enable();
            CurrentControlMode = InputControlMode.Tank;
        } else {
            Cursor.lockState = CursorLockMode.None;
            TankActionMap.Disable();
            TankControlInputs.ResetControlData();
        }
    }

    private void EnableMenuControlMode(bool enable) {
        if(MenuActionMap.enabled == enable) return;

        if (enable) {
            Cursor.lockState = CursorLockMode.None;
            MenuActionMap.Enable();
            CurrentControlMode = InputControlMode.Menu;
        } else {
            MenuActionMap.Disable();
        }
    }

    private void EnableScoreboardControlMode(bool enable) {
        if (ScoreboardActionMap.enabled == enable) return;

        if (enable) {
            Cursor.lockState = CursorLockMode.None;
            ScoreboardActionMap.Enable();
            CurrentControlMode = InputControlMode.Scoreboard;
        } else {
            ScoreboardActionMap.Disable();
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context) {
        TankControlInputs.MoveDelta = context.ReadValue<float>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context) {
        TankControlInputs.MoveDelta = 0.0f;
    }

    private void OnTurnPerformed(InputAction.CallbackContext context) {
        TankControlInputs.TurnDelta = context.ReadValue<float>();
    }

    private void OnTurnCanceled(InputAction.CallbackContext context) {
        TankControlInputs.TurnDelta = 0.0f;
    }

    private void OnLookStarted(InputAction.CallbackContext context) {
        //Debug.Log(context);
    }

    private void OnLookPerformed(InputAction.CallbackContext context) {
        //Debug.Log(context);
        Vector2 rawLookVector = context.ReadValue<Vector2>();

        if (Mathf.Abs(rawLookVector.y) < VERTICAL_LOOK_DEADZONE) {
            rawLookVector.y = 0f;
        }

        if (!(context.control.device is Mouse)) {
            rawLookVector *= GamepadLookSensitivity;
            rawLookVector.y *= GAMEPAD_VERTICAL_LOOK_MULTIPLIER;
        }

        TankControlInputs.LookDelta = rawLookVector;
    }

    private void OnLookCanceled(InputAction.CallbackContext context) {
        //Debug.Log(context);
        TankControlInputs.LookDelta = new Vector2(0, 0);
    }

    private void OnFirePrimaryPerformed(InputAction.CallbackContext context) {
        TankControlInputs.FirePrimary?.Invoke();
    }

    private void OnFireSecondaryPerformed(InputAction.CallbackContext context) {
        TankControlInputs.FireSecondary?.Invoke();
    }

    private void OnSwitchWeaponPerformed(InputAction.CallbackContext context) {
        float inputValue = context.ReadValue<float>();
        if(inputValue < 0f) {
            TankControlInputs.PreviousWeapon?.Invoke();
        } else if(inputValue > 0f) {
            TankControlInputs.NextWeapon?.Invoke();
        }
    }

    private void OnOpenScoreboardPerformed(InputAction.CallbackContext context) {
        SetControlMode(InputControlMode.Scoreboard);
        ShowScoreboardInputChanged?.Invoke(true);
    }

    private void OnCloseScoreboardCanceled(InputAction.CallbackContext context) {
        SetControlMode(InputControlMode.Tank);
        ShowScoreboardInputChanged?.Invoke(false);
    }
}