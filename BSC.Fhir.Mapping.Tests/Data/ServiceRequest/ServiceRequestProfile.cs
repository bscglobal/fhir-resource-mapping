using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
{
    public static StructureDefinition CreateProfile()
    {
        return new()
        {
            Name = "servicerequest-definition",
            Snapshot = new()
            {
                Element =
                {
                    new()
                    {
                        Path = "servicerequest.extension",
                        Slicing = new()
                        {
                            Discriminator = { ElementDefinition.DiscriminatorComponent.ForValueSlice("url") },
                            Rules = ElementDefinition.SlicingRules.Closed
                        },
                    },
                    new() { Path = "servicerequest.extension", SliceName = "careUnit", },
                    new() { Path = "servicerequest.extension.url", Fixed = new FhirString("CareUnitExtension") },
                    new() { Path = "servicerequest.extension", SliceName = "team", },
                    new() { Path = "servicerequest.extension.url", Fixed = new FhirString("TeamExtension") },
                    new()
                    {
                        Path = "servicerequest.performer",
                        Slicing = new()
                        {
                            Discriminator = { ElementDefinition.DiscriminatorComponent.ForValueSlice("url") },
                            Rules = ElementDefinition.SlicingRules.Closed
                        },
                    },
                    new()
                    {
                        Path = "servicerequest.performer",
                        Slicing = new()
                        {
                            Discriminator = { ElementDefinition.DiscriminatorComponent.ForValueSlice("code") },
                            Rules = ElementDefinition.SlicingRules.Closed
                        },
                    },
                }
            }
        };
    }
}
