using JetBrains.ProjectModel;
using JetBrains.ReSharper.Refactorings.Workflow;

namespace ResharperPlugin
{
    [RefactoringWorkflowProvider(typeof(IAddDependencyWorkflowProvider))]
    public class AddDependencyProvider: IAddDependencyWorkflowProvider, IRefactoringWorkflowProvider
    {
        public IRefactoringWorkflow CreateWorkflow(ISolution solution)
        {
            return new AddDependencyRefactoringWorkflow();
        }

        public RefactoringActionGroup ActionGroup
        {
            get { return RefactoringActionGroup.Blessed; }
        }
    }
}