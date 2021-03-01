namespace Book.Comparer.Logic.Configs {
    public interface IBookComparerConfig {
        double LevensteinBorder { get; init; }
        
        double IntersectBorder { get; init; }
    }
}
