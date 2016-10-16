using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using Leap;
using LeapWrapper;

namespace Leap.Unity
{
    /**LeapServiceProvider creates a Controller and supplies Leap Hands and images */
    public class LeapServiceProvider : LeapProvider
    {
        /** Conversion factor for nanoseconds to seconds. */
        protected const float NS_TO_S = 1e-6f;
        /** Conversion factor for seconds to nanoseconds. */
        protected const float S_TO_NS = 1e6f;
        /** How much smoothing to use when calculating the FixedUpdate offset. */
        protected const float FIXED_UPDATE_OFFSET_SMOOTHING_DELAY = 0.1f;

        [SerializeField]
        protected bool _isHeadMounted = true;

        [SerializeField]
        protected LeapVRTemporalWarping _temporalWarping;

        [Header("Device Type")]
        [SerializeField]
        protected bool _overrideDeviceType = false;

        [Tooltip("If overrideDeviceType is enabled, the hand controller will return a device of this type.")]
        [SerializeField]
        protected LeapDeviceType _overrideDeviceTypeWith = LeapDeviceType.Peripheral;

        [Header("Interpolation")]
        [Tooltip("Interpolate frames to deliver a potentially smoother experience.  Currently experimental.")]
        [SerializeField]
        protected bool _useInterpolation = false;

        [Tooltip("How much delay should be added to interpolation.  A non-zero amount is needed to prevent extrapolation artifacts.")]
        [SerializeField]
        protected long _interpolationDelay = 15;

        protected LeapWebSocketController webController;

        protected SmoothedFloat _fixedOffset = new SmoothedFloat();

        protected Frame _untransformedUpdateFrame;
        protected Frame _transformedUpdateFrame;
        protected Image _currentImage;
        protected int _currentUpdateCount = -1;

        protected Frame _untransformedFixedFrame;
        protected Frame _transformedFixedFrame;
        protected float _currentFixedTime = -1;

        private ClockCorrelator clockCorrelator;

        public override Frame CurrentFrame
        {
            get
            {
                updateIfTransformMoved(_untransformedUpdateFrame, ref _transformedUpdateFrame);
                return _transformedUpdateFrame;
            }
        }

        public override Image CurrentImage
        {
            get
            {
                return _currentImage;
            }
        }

        public override Frame CurrentFixedFrame
        {
            get
            {
                updateIfTransformMoved(_untransformedFixedFrame, ref _transformedFixedFrame);
                return _transformedFixedFrame;
            }
        }

        public bool UseInterpolation
        {
            get
            {
                return _useInterpolation;
            }
            set
            {
                _useInterpolation = value;
            }
        }

        public long InterpolationDelay
        {
            get
            {
                return _interpolationDelay;
            }
            set
            {
                _interpolationDelay = value;
            }
        }

        /** Returns the Leap Controller instance. */
        public LeapWebSocketController GetLeapController()
        {
#if UNITY_EDITOR
            //Null check to deal with hot reloading
            if (webController == null)
            {
                createController();
            }
#endif
            return webController;
        }

        /** True, if the Leap Motion hardware is plugged in and this application is connected to the Leap Motion service. */
        public bool IsConnected()
        {
            return GetLeapController().IsConnected;
        }

        /** Returns information describing the device hardware. */
        public LeapDeviceInfo GetDeviceInfo()
        {
            //return new LeapDeviceInfo();
            if (_overrideDeviceType)
            {
                return new LeapDeviceInfo(_overrideDeviceTypeWith);
            }

            DeviceList devices = GetLeapController().Devices;
            Debug.Log("Device count is: " + devices.Count);
            if (devices.Count == 1)
            {

                LeapDeviceInfo info = new LeapDeviceInfo(LeapDeviceType.Peripheral);
                // TODO: DeviceList does not tell us the device type. Dragonfly serial starts with "LE" and peripheral starts with "LP"
                if (devices[0].SerialNumber.Length >= 2)
                {
                    switch (devices[0].SerialNumber.Substring(0, 2))
                    {
                        case ("LP"):
                            info = new LeapDeviceInfo(LeapDeviceType.Peripheral);
                            break;
                        case ("LE"):
                            info = new LeapDeviceInfo(LeapDeviceType.Dragonfly);
                            break;
                        default:
                            break;
                    }
                }

                // TODO: Add baseline & offset when included in API
                // NOTE: Alternative is to use device type since all parameters are invariant
                info.isEmbedded = devices[0].IsEmbedded;
                info.horizontalViewAngle = devices[0].HorizontalViewAngle * Mathf.Rad2Deg;
                info.verticalViewAngle = devices[0].VerticalViewAngle * Mathf.Rad2Deg;
                info.trackingRange = devices[0].Range / 1000f;
                info.serialID = devices[0].SerialNumber;
                return info;
            }
            else if (devices.Count > 1)
            {
                return new LeapDeviceInfo(LeapDeviceType.Peripheral);
            }
            return new LeapDeviceInfo(LeapDeviceType.Invalid);
        }

        protected virtual void Awake()
        {
            clockCorrelator = new ClockCorrelator();
            _fixedOffset.delay = 0.4f;
        }

        protected virtual void Start()
        {
            createController();
            _untransformedUpdateFrame = new Frame();
            _untransformedFixedFrame = new Frame();
            StartCoroutine(waitCoroutine());
        }

        protected IEnumerator waitCoroutine()
        {
            WaitForEndOfFrame endWaiter = new WaitForEndOfFrame();
            while (true)
            {
                yield return endWaiter;
                Int64 unityTime = (Int64)(Time.time * 1e6);
                clockCorrelator.UpdateRebaseEstimate(unityTime);
            }
        }

        protected virtual void Update()
        {
#if UNITY_EDITOR


            if (EditorApplication.isCompiling)
            {
                EditorApplication.isPlaying = false;
                Debug.LogWarning("Unity hot reloading not currently supported. Stopping Editor Playback.");
                return;
            }
#endif

            _fixedOffset.Update(Time.time - Time.fixedTime, Time.deltaTime);

            //if (_useInterpolation)
            //{
            //    Int64 unityTime = (Int64)(Time.time * 1e6);
            //    Int64 unityOffsetTime = unityTime - _interpolationDelay * 1000;
            //    Int64 leapFrameTime = clockCorrelator.ExternalClockToLeapTime(unityOffsetTime);
            //    _untransformedUpdateFrame = webController.GetInterpolatedFrame(leapFrameTime) ?? _untransformedUpdateFrame;
            //}
            //else
            //{
            //    _untransformedUpdateFrame = webController.Frame();
            //}


            if (!webController.IsConnected)
            {
                // Debug.Log("update Not connected");
            }
            _untransformedUpdateFrame = webController.Frame();
            //Null out transformed frame because it is now stale
            //It will be recalculated if it is needed
            _transformedUpdateFrame = null;
        }

        protected virtual void FixedUpdate()
        {
            //if (_useInterpolation)
            //{
            //    Int64 unityTime = (Int64)((Time.fixedTime + _fixedOffset.value) * 1e6);
            //    Int64 unityOffsetTime = unityTime - _interpolationDelay * 1000;
            //    Int64 leapFrameTime = clockCorrelator.ExternalClockToLeapTime(unityOffsetTime);

            //    _untransformedFixedFrame = webController.GetInterpolatedFrame(leapFrameTime) ?? _untransformedFixedFrame;
            //}
            //else
            //{
            //    _untransformedFixedFrame = webController.Frame();
            //}
            if (!webController.IsConnected)
            {
                Debug.Log("Fixedupdate Not connected");
            }
            _untransformedFixedFrame = webController.Frame();
            //     Debug.Log("Fixed Update is hereererererere" + webController.Frame());
            _transformedFixedFrame = null;
        }

        protected virtual void OnDestroy()
        {
            destroyController();
        }

        protected virtual void OnApplicationPause(bool isPaused)
        {
            if (webController != null)
            {
                if (isPaused)
                {
                    webController.StopConnection();
                }
                else
                {
                    webController.StartConnection();
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            destroyController();
        }

        /*
         * Initializes the Leap Motion policy flags.
         * The POLICY_OPTIMIZE_HMD flag improves tracking for head-mounted devices.
         */
        protected void initializeFlags()
        {
            if (webController == null)
            {
                return;
            }
            //Optimize for top-down tracking if on head mounted display.
            if (_isHeadMounted)
            {
                webController.SetPolicy(Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
            }
            else
            {
                webController.ClearPolicy(Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
            }
        }
        /** Create an instance of a Controller, initialize its policy flags
         * and subscribe to connection event */
        protected void createController()
        {
            if (webController != null)
            {
                destroyController();
            }
            webController = FindObjectOfType<LeapWebSocketController>();

            if (webController.IsConnected)
            {
                initializeFlags();
            }
            else
            {
                webController.Device += onHandControllerConnect;
            }
        }

        /** Calling this method stop the connection for the existing instance of a Controller, 
         * clears old policy flags and resets to null */
        protected void destroyController()
        {
            if (webController != null)
            {
                if (webController.IsConnected)
                {
                    webController.ClearPolicy(Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
                }
                webController.StopConnection();
                webController = null;
            }
        }

        protected void onHandControllerConnect(object sender, LeapEventArgs args)
        {
            initializeFlags();
            webController.Device -= onHandControllerConnect;
        }

        protected void updateIfTransformMoved(Frame source, ref Frame toUpdate)
        {
            if (transform.hasChanged)
            {
                _transformedFixedFrame = null;
                _transformedUpdateFrame = null;
                transform.hasChanged = false;
                //Debug.Log("has changed ");
            }

            if (toUpdate == null)
            {
                LeapTransform leapTransform;
                if (_temporalWarping != null)
                {
                    Vector3 warpedPosition;
                    Quaternion warpedRotation;
                    _temporalWarping.TryGetWarpedTransform(LeapVRTemporalWarping.WarpedAnchor.CENTER, out warpedPosition, out warpedRotation, source.Timestamp);

                    warpedRotation = warpedRotation * transform.localRotation;

                    leapTransform = new LeapTransform(warpedPosition.ToVector(), warpedRotation.ToLeapQuaternion(), transform.lossyScale.ToVector() * 1e-3f);
                    leapTransform.MirrorZ();
                }
                else
                {
                    leapTransform = transform.GetLeapMatrix();
                    //Debug.Log("This is the transforme" + leapTransform.translation);
                }
                //     Debug.Log("This is the source" + source);

                //toUpdate = source.TransformedCopy(leapTransform);
                try
                {

                    toUpdate = source.TransformedCopy(leapTransform);

                    //Debug.Log(webController.Frame());
                    //toUpdate = source.TransformedCopy(leapTransform);
                }
                catch (Exception e)
                {
                    //Debug.Log("leaptransform   " + transform);
                    Debug.Log("Type:     " + e + "Message:    " + e.Message + "    stacktrace:" + e.StackTrace);
                }

                //toUpdate = webController.Frame().TransformedCopy(leapTransform);
            }
        }
    }
}
