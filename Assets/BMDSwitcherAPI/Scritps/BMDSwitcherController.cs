using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using UniRx;

namespace BMDSwitcherAPI
{
    public class BMDSwitcherController : MonoBehaviour
    {
        [SerializeField] private string _ipAddress = "192.168.10.240";
        [SerializeField] private string _switcherName = "SwitcherName";
        [SerializeField] private string _programInput = "ProgramInput";
        [SerializeField] private string _previewInput = "PreviewInput";
        [SerializeField, Range(0, 1)] private float _transition = 0;

        private IBMDSwitcherMixEffectBlock _mixEffectBlock = null;
        private Dictionary<string, long> _inputDictionary = new Dictionary<string, long>();
        private long _programId;
        private long _previewId;
        private bool _isConnecting;

        public string IpAddress
        {
            set { _ipAddress = value; }
            get { return _ipAddress; }
        }

        public string SwitcherName
        {
            private set { _switcherName = value; }
            get { return _switcherName; }
        }

        public string ProgramInput
        {
            set { _programInput = value; }
            get { return _programInput; }
        }

        public string PreviewInput
        {
            set { _previewInput = value; }
            get { return _previewInput; }
        }

        public float Transition
        {
            set { _transition = value; }
            get { return _transition; }
        }

        public bool IsConnecting
        {
            private set { _isConnecting = value; }
            get { return _isConnecting; }
        }

        public Dictionary<string, long> InputDictionary
        {
            private set { _inputDictionary = value; }
            get { return _inputDictionary; }
        }

        public async Task<bool> Connect()
        {
            try
            {
                _mixEffectBlock = await Task.Run(() =>
                {
                    return Setup();
                });

                _mixEffectBlock.GetProgramInput(out _programId);
                _mixEffectBlock.GetPreviewInput(out _previewId);

                this.ObserveEveryValueChanged(_ => _._previewId).Subscribe(id => _previewInput = _inputDictionary.FirstOrDefault(_ => _.Value == id).Key).AddTo(gameObject);
                this.ObserveEveryValueChanged(_ => _._programId).Subscribe(id => _programInput = _inputDictionary.FirstOrDefault(_ => _.Value == id).Key).AddTo(gameObject);
                this.ObserveEveryValueChanged(_ => _._transition).Subscribe(value => _mixEffectBlock.SetTransitionPosition(value)).AddTo(gameObject);
                this.ObserveEveryValueChanged(_ => _._programInput).Subscribe(value =>
                {
                    long inputId = 0;
                    _inputDictionary.TryGetValue(value, out inputId);
                    _mixEffectBlock.SetProgramInput(inputId);
                }).AddTo(gameObject);
                this.ObserveEveryValueChanged(_ => _._previewInput).Subscribe(value =>
                {
                    long inputId = 0;
                    _inputDictionary.TryGetValue(value, out inputId);
                    _mixEffectBlock.SetPreviewInput(inputId);
                }).AddTo(gameObject);

                Debug.Log("Connect " + _ipAddress);

                _isConnecting = true;

                return true;
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                Debug.LogError("Not Connect " + _ipAddress);
                Debug.LogException(e);
                return false;
            }
        }

        private async Task<IBMDSwitcherMixEffectBlock> Setup()
        {
            return await Task.Run(() =>
            {
                IBMDSwitcherDiscovery discovery;
                IBMDSwitcher switcher;
                AtemSwitcher atem;
                _BMDSwitcherConnectToFailure failureReason;

                discovery = new CBMDSwitcherDiscovery();
                discovery.ConnectTo(_ipAddress, out switcher, out failureReason);

                atem = new AtemSwitcher(switcher);

                switcher.GetProductName(out _switcherName);

                // // Get reference to various objects
                var mixEffectBlock = atem.MixEffectBlocks.First();

                // Get an input iterator.
                var inputs = atem.SwitcherInputs;

                foreach (var input in inputs)
                {
                    string inputName;
                    long inputId;

                    input.GetInputId(out inputId);
                    input.GetLongName(out inputName);

                    // Add items to list:
                    _inputDictionary.Add(inputName, inputId);
                }

                return mixEffectBlock;
            });
        }

        private void OnDestroy()
        {

        }

        private void Update()
        {
            if (_mixEffectBlock == null) return;

            try
            {
                _mixEffectBlock.GetProgramInput(out _programId);
                _mixEffectBlock.GetPreviewInput(out _previewId);
                _mixEffectBlock.SetFadeToBlackRate(30); // コネクトしているか確認のため適当なセット関数を呼んでいる
            }
            catch
            {
                if (_isConnecting) Disconnected();
            }
        }

        public void PlayAutoMix(uint frame)
        {
            var mix = _mixEffectBlock as IBMDSwitcherTransitionMixParameters;
            mix.SetRate(frame);
            _mixEffectBlock.PerformAutoTransition();
        }

        private void Disconnected()
        {
            _isConnecting = false;
            _inputDictionary.Clear();
            _mixEffectBlock = null;
        }
    }
}