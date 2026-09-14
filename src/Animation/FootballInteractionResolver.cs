using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Presentation;

public static class FootballInteractionResolver
{
    public static FootballInteractionMode Resolve(BallState ball, HumanoidAnimationState animationState)
    {
        if (!ball.PossessingPlayerId.HasValue)
            return FootballInteractionMode.None;
        if (ball.Phase == BallPhase.HeldByQuarterback)
            return animationState == HumanoidAnimationState.Throw
                ? FootballInteractionMode.ThrowingHand
                : FootballInteractionMode.QuarterbackHold;
        if (ball.Phase is BallPhase.Caught or BallPhase.Intercepted)
            return animationState is HumanoidAnimationState.Catch or HumanoidAnimationState.InterceptionCatch
                ? FootballInteractionMode.CatchHands
                : FootballInteractionMode.Carry;
        return FootballInteractionMode.None;
    }
}
