namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IPluralContext<T1, T2> : IQuestionnaireContext<T2>
    where T2 : IEnumerable<T1> { }
