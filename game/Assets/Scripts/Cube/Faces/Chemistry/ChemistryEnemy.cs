using UnityEngine;

namespace Game.Cube
{
    /// <summary>Tags an Enemy as a Chemistry enemy of a kind (Free Radical or Rust Mite).</summary>
    public class ChemistryEnemy : MonoBehaviour
    {
        [SerializeField] private ChemistryEnemyKind kind;

        public ChemistryEnemyKind Kind
        {
            get => kind;
            set => kind = value;
        }
    }
}
