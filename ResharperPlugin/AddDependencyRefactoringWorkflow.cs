using System.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application.Progress;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Refactorings.ChangeSignature;
using JetBrains.ReSharper.Refactorings.Conflicts;
using JetBrains.ReSharper.Refactorings.Workflow;
using JetBrains.TextControl;
using DataConstants = JetBrains.ReSharper.Psi.Services.DataConstants;

namespace ResharperPlugin
{
    public class AddDependencyRefactoringWorkflow: IRefactoringWorkflow
    {
        private AddDependencyModel _model;
        private AddDependencyPage _page;
        private IDeclaredElementPointer<ITypeElement> _class;

        public bool Execute(IProgressIndicator progressIndicator)
        {
            return true;
        }

        public bool Initialize(IDataContext context)
        {
            _model = new AddDependencyModel();
            _page = new AddDependencyPage(_model, context.GetData(JetBrains.IDE.DataConstants.SOLUTION));
            _class = GetClass(context).CreateElementPointer();
            return true;
        }

        public bool IsAvailable(IDataContext context)
        {
            var classElement = GetClass(context);
            if (classElement == null)
                return false;

            if (classElement.Constructors.Count > 1)
                return false;

            var textControl = context.GetData(JetBrains.IDE.DataConstants.TEXT_CONTROL);
            if ((textControl != null) && textControl.Selection.HasSelection())
                return false;

            return true;
        }

        private static ITypeElement GetClass(IDataContext context)
        {
            var declaredElement = context.GetData(DataConstants.DECLARED_ELEMENT);
            var classDecl = declaredElement as ITypeElement;
            if(classDecl == null)
            {
                var parametersOwner = declaredElement as IParametersOwner;
                if (parametersOwner == null)
                    return null;
                var containingType = parametersOwner.GetContainingType();
                if (containingType == null)
                    return null;
                var elementPointer = containingType.CreateElementPointer();
                classDecl = elementPointer.FindDeclaredElement();
            }
            return classDecl;
        }

        public void PostExecute(IProgressIndicator progressIndicator)
        {
        }

        public bool PreExecute(IProgressIndicator progressIndicator)
        {
            return true;
        }

        public bool RecoverAfterExternalChanges(IProgressIndicator progressIndicator)
        {
            progressIndicator.Start(1);
            progressIndicator.Stop();
            return true;
        }

        public void RollbackPreExecute(IProgressIndicator progressIndicator)
        {
        }

        public IConflictSearcher ConflictSearcher
        {
            get 
            {
                return new CompositeConflictSearcher(new IConflictSearcher[0]);
            }
        }

        public IRefactoringPage FirstPendingRefactoringPage
        {
            get { return _page; }
        }

        public bool MightModifyManyDocuments
        {
            get { return true; }
        }

        public string Title
        {
            get { return "Add Dependency"; }
        }

        public bool HasUI
        {
            get { return true; }
        }

        public string HelpKeyword
        {
            get { return "Refactorings__Add_Dependency"; }
        }
    }
}