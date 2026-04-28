namespace GamePlay.Strategy
{
    // 国家间外交状态。默认 Peace=0，外部可通过 DiplomacyIndex.SetRelation 修改。
    public enum DiplomacyStatus : byte
    {
        // 和平状态：不发生战斗。
        Peace = 0,

        // 宣战状态：互为敌方。
        War = 1,

        // 联盟状态：互为友方，不发生战斗。
        Alliance = 2
    }
}
