using System.Collections.Generic;
using System.Linq;
using JetBrains.ActionManagement;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CodeStyle;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Naming;
using JetBrains.ReSharper.Psi.Naming.Extentions;
using JetBrains.ReSharper.Psi.Naming.Settings;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Refactorings.Conflicts;
using JetBrains.ReSharper.Refactorings.Workflow;
using JetBrains.TextControl;
using DataConstants = JetBrains.ReSharper.Psi.Services.DataConstants;

namespace ResharperPlugin
{
    public class AddDependencyRefactoringWorkflow: IRefactoringWorkflow
    {
        private AddDependencyPage _page;
        private IDeclaredElementPointer<ITypeElement> _class;
        private IDeclaredElementPointer<IConstructor> _ctor;
        private ISolution _solution;
        private string _parameterType;

        public bool Execute(IProgressIndicator progressIndicator)
        {
            var ctor = _ctor.FindDeclaredElement();
            if (ctor == null)
                return false;
            var definingClass = _class.FindDeclaredElement();
            if (definingClass == null)
                return false;
            if (ctor.Module == null)
                return false;
            var factory = CSharpElementFactory.GetInstance(ctor.Module);
            var ctorDecl = ctor.GetDeclarations().FirstOrDefault();
            if (ctorDecl == null)
            {
                var typeDecl = definingClass.GetDeclarations().FirstOrDefault() as IClassLikeDeclarationNode;
                if (typeDecl == null)
                    return false;
                var typeBody = typeDecl.Body;
                ctorDecl = factory.CreateTypeMemberDeclaration("public $0() {}", typeDecl.DeclaredName);
                if (typeBody.FirstChild == null)
                    return false;
                if (ctorDecl == null)
                    return false;

                ctorDecl = ModificationUtil.AddChildBefore(
                    typeBody, 
                    typeBody.FirstChild.NextSibling, 
                    ctorDecl.ToTreeNode()).GetContainingElement<IConstructorDeclaration>(true);
            }
            if (ctorDecl == null)
                return false;
            var type = CSharpTypeFactory.CreateType(_parameterType, ctorDecl.ToTreeNode());
            if (!type.IsResolved)
                type = CSharpTypeFactory.CreateType(_parameterType, ctorDecl.GetPsiModule());
            if (!type.IsResolved)
            {
                var interfaceDecl = factory.CreateTypeMemberDeclaration("public interface IFoo {}");
                if (interfaceDecl == null)
                    return false;
                interfaceDecl.SetName(type.GetPresentableName(CSharpLanguageService.CSHARP));
                interfaceDecl.LanguageService.CodeFormatter.Format(interfaceDecl.ToTreeNode(), CodeFormatProfile.GENERATOR);
                var containingType = ctor.GetContainingType();
                if (containingType == null)
                    return false;
                var containingTypeDecl = containingType.GetDeclarations().First();
                ModificationUtil.AddChildBefore(containingTypeDecl.ToTreeNode(), interfaceDecl.ToTreeNode());
            }
            type = CSharpTypeFactory.CreateType(_parameterType, ctorDecl.ToTreeNode());
            var naming = PsiManager.GetInstance(_solution).Naming;
            var suggestionOptions = new SuggestionOptions();
            var recommendedName = naming.Suggestion.GetDerivedName(
                type.GetPresentableName(CSharpLanguageService.CSHARP), 
                NamedElementKinds.Parameters, ScopeKind.Common,
                CSharpLanguageService.CSHARP, suggestionOptions);

            var parametersOwner = ctorDecl as ICSharpParametersOwnerDeclaration;
            var references = FindReferences(parametersOwner, progressIndicator);

            if (parametersOwner == null)
                return false;
            parametersOwner.AddParameterDeclarationAfter(
                ParameterKind.VALUE, type, recommendedName,
                parametersOwner.ParameterDeclarations.LastOrDefault());

            foreach (var reference in references)
                ChangeReference(reference, recommendedName, type);

            return true;
        }

        private static void ChangeReference(IArgumentsOwner reference, string recommendedName, IType type)
        {
            var csharpOwner = reference as ICSharpArgumentsOwner;
            if (csharpOwner == null || type.Module == null)
                return;
            var factory = CSharpElementFactory.GetInstance(type.Module);
            var inField = false;
            if (csharpOwner.GetContainingElement<IFieldDeclaration>(false) != null)
                inField = true;
            var expression = factory.CreateExpression(inField ? "TODO" : recommendedName);
            csharpOwner.AddArgumentAfter(
                factory.CreateArgument(ParameterKind.VALUE, null, expression),
                csharpOwner.Arguments.LastOrDefault());
        }

        private static IEnumerable<IArgumentsOwner> FindReferences(ICSharpParametersOwnerDeclaration parametersOwner, IProgressIndicator progressIndicator)
        {
            var references = new List<IArgumentsOwner>();
            var consumer = new FindResultConsumer(
                r =>
                {
                    var owners = new HashSet<IArgumentsOwner>();
                    var reference = r as FindResultReference;
                    if(reference != null)
                    {
                        var ref2 = reference.Reference;
                        var resolveType = ref2.CheckResolveResult();
                        if (resolveType == ResolveErrorType.INCORRECT_PARAMETER_NUMBER)
                            return FindExecution.Continue;
                        var argumentsOwner = ref2.GetElement().GetContainingElement<IArgumentsOwner>(true);
                        if(argumentsOwner != null)
                        {
                            if (owners.Contains(argumentsOwner))
                                return FindExecution.Continue;
                            owners.Add(argumentsOwner);
                            references.Add(argumentsOwner);
                        }
                    }
                    return FindExecution.Continue;
                });
            var searchAction = new SearchAction(
                parametersOwner.GetManager().Finder,
                parametersOwner.DeclaredElement,
                consumer,
                SearchPattern.FIND_USAGES);
            searchAction.Task(progressIndicator);
            return references;
        }

        public bool Initialize(IDataContext context)
        {
            _solution = context.GetData(JetBrains.IDE.DataConstants.SOLUTION);
            _page = new AddDependencyPage(
                s => _parameterType = s, 
                context.GetData(JetBrains.IDE.DataConstants.SOLUTION));
            var @class = GetClass(context);
            _class = @class.CreateElementPointer();
            var ctor = @class.Constructors.FirstOrDefault();
            if (ctor != null)
                _ctor = ctor.CreateElementPointer();
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