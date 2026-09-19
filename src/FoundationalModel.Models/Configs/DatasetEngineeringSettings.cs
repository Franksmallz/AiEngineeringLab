namespace FoundationalModel.Models.Configs
{
    public class DatasetEngineeringSettings
    {
        public const string SectionName = "Dataset";
        public string DataDirectory { get; set; } = "finetuning/data";
        public string TrainingDataFileName { get; set; } = "train.jsonl";
    }
}