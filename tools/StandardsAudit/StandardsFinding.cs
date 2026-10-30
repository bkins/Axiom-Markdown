namespace Axiom.StandardsAudit;

internal sealed record StandardsFinding(string File
                                      , int    Line
                                      , string Rule
                                      , string Message);
