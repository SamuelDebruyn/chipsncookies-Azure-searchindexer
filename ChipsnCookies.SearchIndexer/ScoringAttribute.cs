using System;

namespace ChipsnCookies.SearchIndexer
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ScoringAttribute: Attribute
    {
        public double Weight { get; }

        public ScoringAttribute(double weight)
        {
            Weight = weight;
        }

        public override string ToString() => "Weight: " + Weight;
    }
}
