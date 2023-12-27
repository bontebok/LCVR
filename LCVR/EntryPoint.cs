﻿using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;
using System.Collections;
using LCVR.Networking;
using LCVR.Patches;
using LCVR.Player;
using LCVR.Assets;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using UnityEngine.InputSystem.UI;
using LCVR.Input;

namespace LCVR
{
    [LCVRPatch]
    [HarmonyPatch]
    internal class VREntryPoint
    {
        /// <summary>
        /// The entrypoint for when you join a game
        /// </summary>
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        private static void OnGameEntered()
        {
            StartOfRound.Instance.StartCoroutine(Start());
        }
    
        private static IEnumerator Start()
        {
            Logger.Log("Hello from VR!");

            yield return new WaitUntil(() => StartOfRound.Instance.activeCamera != null);

            var mainCamera = StartOfRound.Instance.activeCamera;
            var uiCamera = GameObject.Find("UICamera")?.GetComponent<Camera>();

            if (uiCamera == null)
            {
                Logger.LogError("Could not find UI Camera!");
                yield break;
            }

            // Disable base UI input system
            var input = GameObject.Find("EventSystem")?.GetComponent<InputSystemUIInputModule>();

            if (input != null)
                input.enabled = false;

            // Disable first person helmet
            GameObject.Find("PlayerHUDHelmetModel").SetActive(false);

            // Disable ui camera and promote main camera
            mainCamera.targetTexture = null;
            uiCamera.GetComponent<HDAdditionalCameraData>().xrRendering = false;
            uiCamera.stereoTargetEye = StereoTargetEyeMask.None;

            mainCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            mainCamera.GetComponent<HDAdditionalCameraData>().xrRendering = true;

            mainCamera.depth = uiCamera.depth + 1;

            // Apply optimization configuration
            var hdCamera = mainCamera.GetComponent<HDAdditionalCameraData>();
            hdCamera.allowDynamicResolution = Plugin.Config.EnableUpscaling.Value;
            hdCamera.allowDeepLearningSuperSampling = Plugin.Config.EnableDLLS.Value;

            Utils.DisableQualitySetting(hdCamera, FrameSettingsField.DepthOfField);

            if (Plugin.Config.DisableVolumetrics.Value)
                Utils.DisableQualitySetting(hdCamera, FrameSettingsField.Volumetrics);

            // Manually walk to the player object because in multiplayer you are not "Player" but instead one of the other player objects
            var player = mainCamera.gameObject.transform.parent.parent.parent.parent.gameObject.AddComponent<VRPlayer>();

            // Initialize HUD
            player.hud = new GameObject("VR HUD Manager").AddComponent<VRHUD>();
            player.hud.Initialize(player);

            // Temporary: Update item offsets for certain items
            // Will eventually be replaced by VR interactions (two hand holding 'n stuff)
            // TODO: Implement VR interactions
            Player.Items.UpdateVRControlsItemsOffsets();

            // Add VR keyboard to the Terminal
            var terminal = Object.FindObjectOfType<Terminal>();

            var keyboardObject = Object.Instantiate(AssetManager.keyboard);
            keyboardObject.transform.SetParent(terminal.transform.parent.parent, false);
            keyboardObject.transform.localPosition = new Vector3(-0.584f, 0.333f, 0.791f);
            keyboardObject.transform.localEulerAngles = new Vector3(0, 90, 90);
            keyboardObject.transform.localScale = Vector3.one * 0.0009f;

            keyboardObject.GetComponent<Canvas>().worldCamera = uiCamera;

            var keyboard = keyboardObject.GetComponent<NonNativeKeyboard>();
            keyboard.InputField = terminal.screenText;

            keyboard.OnKeyboardValueKeyPressed += (_) =>
            {
                RoundManager.PlayRandomClip(terminal.terminalAudio, terminal.keyboardClips);
            };

            keyboard.OnKeyboardFunctionKeyPressed += (_) =>
            {
                RoundManager.PlayRandomClip(terminal.terminalAudio, terminal.keyboardClips);
            };

            keyboard.OnTextSubmitted += (_, _) =>
            {
                terminal.OnSubmit();
            };

            keyboard.OnClosed += (_, _) =>
            {
                terminal.QuitTerminal();
            };

            Actions.ReloadInputBindings();
        }
    }

    [LCVRPatch(LCVRPatchTarget.Universal)]
    [HarmonyPatch]
    internal class UniversalEntryPoint
    {
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        private static void OnGameEntered()
        {
            StartOfRound.Instance.StartCoroutine(Start());
        }

        private static IEnumerator Start()
        {
            Logger.Log("Hello from universal!");

            yield return new WaitUntil(() => StartOfRound.Instance.activeCamera != null);

            // Setup Dissonance for VR movement comms
            DNet.SetupDissonanceNetworking();
        }

        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnGameLeave()
        {
            DNet.DestroyDissonanceNetworking();
        }
    }
}