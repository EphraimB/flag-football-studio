using System;

namespace FlagFootballStudio.Domain;

public sealed class Game
{
    public Game(Team gold, Team navy)
    {
        Gold = gold ?? throw new ArgumentNullException(nameof(gold));
        Navy = navy ?? throw new ArgumentNullException(nameof(navy));
    }

    public Team Gold { get; }
    public Team Navy { get; }

    public static Game CreatePrototype()
    {
        var gold = new Team(Guid.NewGuid(), "Gold");
        gold.AddPlayer(new Player(Guid.NewGuid(), "Casey", 55, PlayerPosition.Center));
        gold.AddPlayer(new Player(Guid.NewGuid(), "Jordan", 7, PlayerPosition.Quarterback));
        gold.AddPlayer(new Player(Guid.NewGuid(), "Riley", 11, PlayerPosition.Receiver));
        gold.AddPlayer(new Player(Guid.NewGuid(), "Morgan", 18, PlayerPosition.SlotReceiver));
        gold.AddPlayer(new Player(Guid.NewGuid(), "Avery", 22, PlayerPosition.RunningBack));

        var navy = new Team(Guid.NewGuid(), "Navy");
        navy.AddPlayer(new Player(Guid.NewGuid(), "Taylor", 2, PlayerPosition.Defender));
        navy.AddPlayer(new Player(Guid.NewGuid(), "Cameron", 5, PlayerPosition.Defender));
        navy.AddPlayer(new Player(Guid.NewGuid(), "Drew", 14, PlayerPosition.Defender));
        navy.AddPlayer(new Player(Guid.NewGuid(), "Quinn", 24, PlayerPosition.Defender));
        navy.AddPlayer(new Player(Guid.NewGuid(), "Skyler", 31, PlayerPosition.Defender));

        return new Game(gold, navy);
    }
}
