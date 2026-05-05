using System.Xml;
namespace ShelteredAPI.Scenarios.Infrastructure.Serialization{
    internal interface IScenarioSectionSerializer<TSection>
    {
        TSection Read(XmlElement element);
        void Write(XmlWriter writer, TSection value);
    }
}
