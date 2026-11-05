using UnityEngine;

namespace OzhogiTools.Editor
{
    [CreateAssetMenu(fileName = "New Animation Preset", menuName = "OzhogiTools/Animation Preset")]
    public class AnimationPreset : ScriptableObject
    {
        public string[] rootBones = new string[] { "Hips" };
    }
}