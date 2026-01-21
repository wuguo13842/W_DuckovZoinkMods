using UnityEngine;

namespace MiniMap.Poi
{
    public class CharacterPointOfInterest : CharacterPoiBase
    {
        private Color color = Color.white;

        public override Color Color { get => color; set => color = value; }
    }
}
