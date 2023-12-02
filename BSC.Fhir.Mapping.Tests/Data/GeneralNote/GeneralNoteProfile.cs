using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static StructureDefinition CreateProfile()
    {
        return new()
        {
            Name = "composition-definition",
            Snapshot = new()
            {
                Element =
                {
                    new()
                    {
                        Path = "composition.section",
                        Slicing = new()
                        {
                            Discriminator = { ElementDefinition.DiscriminatorComponent.ForValueSlice("code") },
                            Rules = ElementDefinition.SlicingRules.Closed
                        },
                        Min = 2,
                        Max = "2"
                    },
                    new()
                    {
                        Path = "composition.section",
                        SliceName = "note",
                        Min = 1,
                        Max = "1"
                    },
                    new()
                    {
                        Path = "composition.section.code",
                        Min = 1,
                        Fixed = new CodeableConcept
                        {
                            Coding =
                            {
                                new()
                                {
                                    System = "https://1beat.care/fhir/coding-system",
                                    Code = "12345",
                                    Display = "Note"
                                }
                            }
                        }
                    },
                    new()
                    {
                        Path = "composition.section",
                        SliceName = "images",
                        Min = 1,
                        Max = "1"
                    },
                    new()
                    {
                        Path = "composition.section.code",
                        Min = 1,
                        Fixed = new CodeableConcept
                        {
                            Coding =
                            {
                                new()
                                {
                                    System = "https://1beat.care/fhir/coding-system",
                                    Code = "54321",
                                    Display = "Images"
                                }
                            }
                        }
                    },
                    new() { Path = "composition.extension", ElementId = ":extraField" }
                }
            }
        };
    }
}
