using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class Demographics
{
    public static QuestionnaireResponse PopulationResponse(string patientId, (string, string, string) relativeIds)
    {
        return new QuestionnaireResponse
        {
            Item =
            {
                new() { LinkId = "patient.id", Answer = { new() { Value = new FhirString(patientId) } } },
                new() { LinkId = "patient.birthDate", Answer = { new() { Value = new Date("2006-04-05") } } },
                new()
                {
                    LinkId = "patient.name",
                    Item =
                    {
                        new()
                        {
                            LinkId = "patient.name.family",
                            Answer = { new() { Value = new FhirString("Smith") } }
                        },
                        new()
                        {
                            LinkId = "patient.name.given",
                            Answer =
                            {
                                new() { Value = new FhirString("Jane") },
                                new() { Value = new FhirString("Rebecca") },
                            }
                        },
                    }
                },
                new()
                {
                    LinkId = "patient.name",
                    Item =
                    {
                        new()
                        {
                            LinkId = "patient.name.family",
                            Answer = { new() { Value = new FhirString("Stanton") } }
                        },
                        new()
                        {
                            LinkId = "patient.name.given",
                            Answer =
                            {
                                new() { Value = new FhirString("Elisabeth") },
                                new() { Value = new FhirString("Charlotte") },
                            }
                        },
                    }
                },
                new() { LinkId = "patient.gender" },
                new() { LinkId = "patient.active", Answer = { new() { Value = new FhirBoolean(true) } } },
                new()
                {
                    LinkId = "relative",
                    Item =
                    {
                        new()
                        {
                            LinkId = "relative.id",
                            Answer = { new() { Value = new FhirString(relativeIds.Item1) } }
                        },
                        new()
                        {
                            LinkId = "relative.patient",
                            Answer = { new() { Value = new ResourceReference($"Patient/{patientId}") } }
                        },
                        new() { LinkId = "relative.relationship" },
                        new()
                        {
                            LinkId = "relative.name",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "relative.name.family",
                                    Answer = { new() { Value = new FhirString("Paul") } }
                                },
                                new()
                                {
                                    LinkId = "relative.name.given",
                                    Answer = { new() { Value = new FhirString("Annabel") }, }
                                },
                            }
                        },
                    }
                },
                new()
                {
                    LinkId = "relative",
                    Item =
                    {
                        new()
                        {
                            LinkId = "relative.id",
                            Answer = { new() { Value = new FhirString(relativeIds.Item2) } }
                        },
                        new()
                        {
                            LinkId = "relative.patient",
                            Answer = { new() { Value = new ResourceReference($"Patient/{patientId}") } }
                        },
                        new() { LinkId = "relative.relationship" },
                        new()
                        {
                            LinkId = "relative.name",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "relative.name.family",
                                    Answer = { new() { Value = new FhirString("Rutherford") } }
                                },
                                new()
                                {
                                    LinkId = "relative.name.given",
                                    Answer = { new() { Value = new FhirString("Annette") }, }
                                },
                            }
                        },
                    }
                },
                new()
                {
                    LinkId = "relative",
                    Item =
                    {
                        new()
                        {
                            LinkId = "relative.id",
                            Answer = { new() { Value = new FhirString(relativeIds.Item3) } }
                        },
                        new()
                        {
                            LinkId = "relative.patient",
                            Answer = { new() { Value = new ResourceReference($"Patient/{patientId}") } }
                        },
                        new() { LinkId = "relative.relationship" },
                        new()
                        {
                            LinkId = "relative.name",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "relative.name.family",
                                    Answer = { new() { Value = new FhirString("Wesley") } }
                                },
                                new()
                                {
                                    LinkId = "relative.name.given",
                                    Answer = { new() { Value = new FhirString("Schuster") }, }
                                },
                            }
                        },
                    }
                },
            }
        };
    }
}
