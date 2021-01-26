namespace Book.Comparer.Logic.Configs {
    public interface IBookComparerConfig {
        double LevensteinBorder { get; set; }
        
        double IntersectBorder { get; set; }
    }
}
