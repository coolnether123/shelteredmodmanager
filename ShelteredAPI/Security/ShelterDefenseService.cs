namespace ShelteredAPI.Security
{
    public sealed class ShelterDefenseService
    {
        private readonly ShelterDefenseRatingCalculator _calculator;

        public ShelterDefenseService()
            : this(new ShelterDefenseRatingCalculator())
        {
        }

        public ShelterDefenseService(ShelterDefenseRatingCalculator calculator)
        {
            _calculator = calculator ?? new ShelterDefenseRatingCalculator();
        }

        public ShelterDefenseRating Calculate(ShelterDefenseInput input)
        {
            return _calculator.Calculate(input);
        }
    }
}
