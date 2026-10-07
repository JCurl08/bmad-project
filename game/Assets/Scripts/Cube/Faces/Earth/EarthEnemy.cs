using UnityEngine;

namespace Game.Cube
{
    /// <summary>Tags an Enemy as an Earth enemy of a kind.</summary>
    public class EarthEnemy : MonoBehaviour
    {
        [SerializeField] private EarthEnemyKind kind;

        public EarthEnemyKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }
}
