using UnityEngine;

namespace Game.Cube
{
    /// <summary>Tags an Enemy as a Biology enemy of a kind (Seed Weevil or Pollen Puff).</summary>
    public class BiologyEnemy : MonoBehaviour
    {
        [SerializeField] private BiologyEnemyKind kind;

        public BiologyEnemyKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }
}
