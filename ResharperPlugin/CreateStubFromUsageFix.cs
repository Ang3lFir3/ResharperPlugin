using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon.CSharp.Stages;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Intentions;
using JetBrains.ReSharper.Intentions.CSharp.Util;
using JetBrains.ReSharper.Psi.CodeStyle;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.ExpectedTypes;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Services;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace ResharperPlugin
{
    [QuickFix(0x7FFF)]
    public class CreateStubFromUsageFix : BulbItemImpl, IQuickFix
    {
        private readonly IReference _myReference;
        private readonly IReferenceExpression _myReferenceExpression;
        private readonly IBlock _anchor;

        public CreateStubFromUsageFix(NotResolvedError error)
        {
            _myReference = error.Reference;
            _myReferenceExpression = GetReferenceExpression();
            _anchor = ContainingElement<IBlock>();
        }

        public override string Text
        {
            get { return string.Format("Create RhinoMocks stub '{0}'", _myReference.GetName()); }
        }

        public bool IsAvailable(IUserDataHolder cache)
        {
            return _myReferenceExpression != null 
                && _anchor != null
                && !IsInvocationExpression(_myReferenceExpression) 
                && !InsideConstantExpression 
                && IsUnqualifiedExpression 
                && IsUnresolvedOrWrongNameCase();
        }

        public override IBulbItem[] Items
        {
            get { return new[] { this }; }
        }

        protected override Action<ITextControl> ExecuteTransaction(ISolution solution, IProgressIndicator progress)
        {
            var usages = CollectUsages(_anchor);
            var typeConstraint = GuessTypesForUsages(usages);
            
            var declarationStatement = GetDeclarationStatement(
                usages, 
                typeConstraint);
            
            FormatCode(declarationStatement);

            var localVariableDeclaration = declarationStatement.VariableDeclarations[0];
            var typeUsage = localVariableDeclaration.ToTreeNode().TypeUsage;
            var selectionStartOffset = localVariableDeclaration.GetNameDocumentRange().TextRange.EndOffset;
            var selectionRange = new TextRange(selectionStartOffset);
            
            return control => CSharpTemplateUtil.ExecuteTemplate(
                solution,
                control,
                typeConstraint,
                typeUsage,
                true,
                selectionRange);
        }

        private static IExpectedTypeConstraint GuessTypesForUsages(IList<ICSharpExpression> usages)
        {
            return ExpectedTypesUtil.GuessTypes(
                usages.ConvertList<ICSharpExpression, IExpression>());
        }

        private static void FormatCode(IDeclarationStatement declarationStatement)
        {
            declarationStatement.LanguageService.CodeFormatter.Format(
                declarationStatement.ToTreeNode(),
                CodeFormatProfile.GENERATOR);
        }

        private IReferenceExpression GetReferenceExpression()
        {
            return _myReference != null 
                ? _myReference.GetElement() as IReferenceExpression 
                : null;
        }

        private bool IsUnresolvedOrWrongNameCase()
        {
            var type = _myReference.CheckResolveResult();
            return type == ResolveErrorType.NOT_RESOLVED
                || type == ResolveErrorType.WRONG_NAME_CASE;
        }

        private T ContainingElement<T>() where T : class, IElement
        {
            return _myReferenceExpression != null
                ? _myReferenceExpression.GetContainingElement<T>(false)
                : null;
        }

        private bool InsideConstantExpression
        {
            get { return ContainingElement<IGotoCaseStatement>() != null; }
        }

        private bool IsUnqualifiedExpression
        {
            get { return (_myReferenceExpression.QualifierExpression == null); }
        }

        private IList<ICSharpExpression> CollectUsages(IElement scope)
        {
            var elementsWithUnresolvedReferences = CollectElementsWithUnresolvedReferences(
                scope, 
                _myReference.GetName());

            return FilterUsages(elementsWithUnresolvedReferences.Cast<ICSharpExpression>());
        }

        private static IEnumerable<IReferenceExpression> CollectElementsWithUnresolvedReferences(IElement scope, string referenceName)
        {
            return ReferencesCollectingUtil
                .CollectElementsWithUnresolvedReference<IReferenceExpression>(
                    scope,
                    referenceName,
                    x => x.Reference);
        }

        private static IList<ICSharpExpression> FilterUsages(IEnumerable<ICSharpExpression> expressions)
        {
            return expressions.Where(x => !IsInvocationExpression(x)).ToList();
        }

        private static bool IsInvocationExpression(ICSharpExpression expression)
        {
            return InvocationExpressionNavigator.GetByInvokedExpression(expression) != null;
        }

        private IDeclarationStatement GetDeclarationStatement(IEnumerable<ICSharpExpression> usages, IExpectedTypeConstraint typeConstraint)
        {
            try
            {
                var factory = CSharpElementFactory.GetInstance(_myReferenceExpression.GetPsiModule());
                var insertionLocation = ExpressionUtil.GetStatementToBeVisibleFromAll(usages);
                var statement = CreateStubDeclaration(factory, typeConstraint);
                return StatementUtil.InsertStatement(statement, ref insertionLocation, true);
            }
            catch (Exception ex)
            {
                File.AppendAllText("c:\\temp\\MillimanPluginErrors.txt", "Exception on " + DateTime.Now + "\n" + ex + "\n\n");
                throw;
            }
        }

        private IDeclarationStatement CreateStubDeclaration(CSharpElementFactory factory, IExpectedTypeConstraint typeConstraint)
        {
            return (IDeclarationStatement)factory.CreateStatement(
                "var $0 = MockRepository.GenerateStub<$1>();",
                _myReference.GetName(),
                GetStubInterfaceName(typeConstraint));
        }

        private string GetStubInterfaceName(IExpectedTypeConstraint typeConstraint)
        {
            var languageType = _anchor.Language;
            var firstInterfaceType = typeConstraint.GetDefaultTypes().First();
            return firstInterfaceType.GetPresentableName(languageType);
        }
    }
}