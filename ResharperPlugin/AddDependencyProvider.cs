using JetBrains.ProjectModel;
using JetBrains.ReSharper.Refactorings.Workflow;

namespace ResharperPlugin
{
    public interface IAddDependencyWorkflowProvider : IRefactoringWorkflowProvider
    {
    }
    
    [RefactoringWorkflowProvider(typeof(IAddDependencyWorkflowProvider))]
    public class AddDependencyProvider: IAddDependencyWorkflowProvider
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