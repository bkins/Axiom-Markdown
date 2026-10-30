using System.Globalization;
using System.Text.RegularExpressions;

namespace Axiom.Core;

public enum MarkdownListEditKind
{
    Continue
  , Indent
  , Outdent
}
