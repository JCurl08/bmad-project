using UnityEngine;

namespace Game.Cube
{
    /// <summary>Tags an Enemy as a Math enemy of a kind.</summary>
    public class MathEnemy : MonoBehaviour
    {
        [SerializeField] private MathEnemyKind kind;

        public MathEnemyKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }
}
