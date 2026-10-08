namespace Game.Cube
{
    /// <summary>
    /// A whole-screen puzzle's runtime object that holds its reward roadblock (4.10; 4.11's wind screens too): CubeWorld builds
    /// the chest on the puzzle's goal (RewardPlacement's PuzzleGoal) and hands it over here, so the puzzle can keep it shut
    /// (RewardChest.Guard) until its goal is reached.
    /// </summary>
    public interface IPuzzleRewardHost
    {
        void AttachReward(RewardChest chest);
    }
}
