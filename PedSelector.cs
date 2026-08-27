using GTA;

namespace gta.Peds
{
    internal static class PedSelector
    {
        private static readonly PedHash[] ProstituteModels =
        {
            PedHash.Stripper01Cutscene,
            PedHash.Stripper01SFY,
            PedHash.StripperLite,
            PedHash.Stripper02Cutscene,
            PedHash.Stripper02SFY,
            PedHash.StripperLiteSFY
        };

        private static readonly System.Random Random = new System.Random();

        public static PedHash GetRandomProstituteModel()
        {
            var index = Random.Next(ProstituteModels.Length);
            return ProstituteModels[index];
        }
    }
}
