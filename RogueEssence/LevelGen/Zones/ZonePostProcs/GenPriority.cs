﻿using System;
using RogueElements;

namespace RogueEssence.LevelGen
{
    [Serializable]
    public class GenPriority<T> : IGenPriority where T : IGenStep
    {
        public Priority Priority { get; set; }
        public T Item;

        public GenPriority() { }
        public GenPriority(T effect)
        {
            Item = effect;
        }
        public GenPriority(Priority priority, T effect)
        {
            Priority = priority;
            Item = effect;
        }

        public IGenStep GetItem() { return Item; }
    }

    public interface IGenPriority
    {
        Priority Priority { get; set; }
        IGenStep GetItem();
    }
}
