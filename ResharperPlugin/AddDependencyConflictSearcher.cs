using System.Collections.Generic;
using JetBrains.Application.Progress;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Refactorings.Conflicts;

namespace ResharperPlugin
{
    public class AddDependencyConflictSearcher: IConflictSearcher
    {
        public AddDependencyConflictSearcher(AddDependencyModel model, List<IReference> references)
        {
        }

        public ConflictSearchResult SearchConflicts(IProgressIndicator progressIndicator, bool canPerformRefactoring)
        {
            // TODO: needs implementation
            return new ConflictSearchResult();
        }
    }
}