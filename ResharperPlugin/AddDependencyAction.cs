using JetBrains.ActionManagement;
using JetBrains.ReSharper.Refactorings.Workflow;
using JetBrains.UI.RichText;

namespace ResharperPlugin
{
#pragma warning disable 612,618
    [ActionHandler("AddDependency")]
#pragma warning restore 612,618
    public class AddDependencyAction: ExtensibleRefactoringAction<IAddDependencyWorkflowProvider>
    {
        protected override RichText GetGroupCaption()
        {
            return "Add Dependency";
        }
    }
}