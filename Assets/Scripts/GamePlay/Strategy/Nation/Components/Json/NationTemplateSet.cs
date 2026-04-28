#region

using Core.Json;

#endregion

namespace GamePlay.Strategy
{
    // 国家模板集合，负责通过项目资源链读取 Config://Nation 下的所有国家 JSON。
    public class NationTemplateSet : JsonTemplateSet<NationTemplateSet, NationTemplate>
    {
        protected override string ConfigDir => "Config://Nation";
    }
}
