#region

using System.Collections.Generic;
using Faction.Data;
using GamePlay.Nation;
using Tool;

#endregion

namespace GamePlay.Faction
{
    public class NationManager : SingletonMono<NationManager>
    {
        public List<NationData> Nations = new();

        private void Start()
        {
            foreach (var nation in Nations)
            {
                NationBehaviour.Occupy(nation.Id, nation.Hexs[0]);
            }
        }
    }
}