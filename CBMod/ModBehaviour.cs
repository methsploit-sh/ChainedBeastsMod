using System;
using UnityEngine;

namespace CBMod
{
    // The component the game runs. It holds no logic: everything is in Controller,
    // so the IL2CPP-injected class stays trivial.
    public class ModBehaviour : MonoBehaviour
    {
        // Constructor required by Il2CppInterop.
        public ModBehaviour(IntPtr ptr) : base(ptr) { }

        private void Start() { Controller.Start(); }
        private void Update() { Controller.Update(); }
        private void LateUpdate() { Controller.LateUpdate(); }
        private void OnGUI() { Controller.OnGui(); }
    }
}
