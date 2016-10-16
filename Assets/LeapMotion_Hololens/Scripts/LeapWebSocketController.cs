
using UnityEngine;
using System;
using Leap;


namespace LeapWrapper
{
    public class LeapWebSocketController : MonoBehaviour, IController
    {
        public LeapWebProcessor processor;
        public DeviceList Devices { get; internal set; }
        public event EventHandler<DeviceEventArgs> Device;
        public System.Func<object> Connect { get; internal set; }
        public Action<object, LeapEventArgs> DistortionChange { get; internal set; }
        public event EventHandler<ConnectionLostEventArgs> Disconnect;
        public event EventHandler<FrameEventArgs> FrameReady;
        public event EventHandler<DeviceEventArgs> DeviceLost;
        public event EventHandler<ImageEventArgs> ImageReady;
        public event EventHandler<DeviceFailureEventArgs> DeviceFailure;
        public event EventHandler<LogEventArgs> LogMessage;
        public event EventHandler<PolicyEventArgs> PolicyChange;    
        public event EventHandler<ConfigChangeEventArgs> ConfigChange;

        void Start()
        {

        }

        void OnDestroy()
        {

            StopConnection();
        }

        public Config Config
        {
            get
            {
                return null;
            }
        }

        public bool IsConnected
        {
            get
            {
                return processor.IsConnected;
            }
        }

        event EventHandler<ConnectionEventArgs> IController.Connect
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<DeviceEventArgs> IController.Device
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler<DistortionEventArgs> IController.DistortionChange
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public Frame Frame(int history = 0)
        {
            return processor.frame;
        }

        public Frame GetTransformedFrame(LeapTransform trs, int history = 0)
        {
            return processor.frame.TransformedCopy(trs);
        }

        public Frame GetInterpolatedFrame(long time)
        {
            return processor.frame;
        }

        public void SetPolicy(Controller.PolicyFlag policy)
        {
            processor.SetPolicy(policy);
        }


        public void ClearPolicy(Controller.PolicyFlag policy)
        {
            processor.SetPolicy(policy);

        }

        public bool IsPolicySet(Controller.PolicyFlag policy)
        {
            Debug.Log("SetPolicy: IsPolicySet");
            return true;
        }

        public long Now()
        {
            return processor.timestamp;
        }

        public void Dispose()
        {
            return;
        }

        internal void StopConnection()
        {
            processor.StopConnection();
        }

        internal void StartConnection()
        {
            processor.StartConnection();
        }



    }
}


