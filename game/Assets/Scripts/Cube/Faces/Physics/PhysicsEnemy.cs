using UnityEngine;

namespace Game.Cube
{
    /// <summary>Tags an Enemy as a Physics enemy of a kind (Quantum Flea or Static Cling).</summary>
    public class PhysicsEnemy : MonoBehaviour
    {
        [SerializeField] private PhysicsEnemyKind kind;

        public PhysicsEnemyKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }
}
