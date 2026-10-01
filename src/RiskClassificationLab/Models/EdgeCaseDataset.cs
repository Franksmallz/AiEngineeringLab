namespace RiskClassificationLab.Models
{
    public class EdgeCaseDataset
    {
        public static TransactionRiskInput AmountBoundaryBelow()
        {
            return new TransactionRiskInput
            {
                Amount = 7499,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 2,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput AmountBoundaryAbove()
        {
            return new TransactionRiskInput
            {
                Amount = 7501,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 2,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput TransactionCountBoundaryAbove()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 21,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput TransactionCountBoundaryBelow()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 20,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput RecentFailureCountBoundaryAbove()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 3,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput RecentFailureCountBoundaryBelow()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 2,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput BeneficiaryAgeDaysBoundaryAbove()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 7,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput BeneficiaryAgeDaysBoundaryBelow()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 12,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 6,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput TransactionHourBoundaryAbove()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 5,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }

        public static TransactionRiskInput TransactionHourBoundaryBelow()
        {
            return new TransactionRiskInput
            {
                Amount = 5000,
                TransactionHour = 4,
                CustomerTransactionCount24h = 10,
                RecentFailureCount = 1,
                BeneficiaryAgeDays = 30,
                IsHighRiskCountry = false
            };
        }
    }
}
