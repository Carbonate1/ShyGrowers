using System;
using Godot;

public class AdjacencyBonus
{
    public enum Type
    {
        None,
        GrowthTime,
        DecayTime,
        SaleValue,
        AdditionalSeason
    }

    public readonly Type type;
    public readonly Season season;
    public readonly int multiplier;

    public AdjacencyBonus(
        Type type,
        Season season,
        int multiplier
    )
    {
        if (type == Type.None && (multiplier != 0 || season != Season.None))
        {
            throw new ArgumentException("If type == none, multiplier must be 0 and season must be none");
        } 
        if (season != Season.None && multiplier != 0)
        {
            throw new ArgumentException("If season != none, multiplier must be 0");
        }
        if (type == Type.AdditionalSeason && (season == Season.None || season == Season.Any)) {
            throw new ArgumentException("If type == AdditionalSeason, season must not be none nor any");
        }

        this.type = type;
        this.season = season;
        this.multiplier = multiplier;
    }

    public AdjacencyBonus(Type type = Type.None)
    {
        this.type = type;
        season = Season.None;
        multiplier = 0;
    }
}