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

namespace MillimanPlugin
{
    [QuickFix(0x7FFF)]
    public class CreateStubFromUsageFix : BulbItemImpl, IQuickFix
    {
        // Fields
        private readonly IReference _myReference;
        private readonly IReferenceExpression _myReferenceExpression;
        private readonly IBlock _anchor;

        // Methods
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

        #region IQuickFix Members

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

        #endregion

        protected override Action<ITextControl> ExecuteTransaction(ISolution solution, IProgressIndicator progress)
        {
            var instance = CSharpElementFactory.GetInstance(_myReferenceExpression.GetPsiModule());
            var usages = CollectUsages(_anchor);
            var typeConstraint = ExpectedTypesUtil.GuessTypes(usages.ConvertList<ICSharpExpression, IExpression>());
            var statementToBeVisibleFromAll = ExpressionUtil.GetStatementToBeVisibleFromAll(usages);
            var declarationStatement = GetDeclarationStatement(statementToBeVisibleFromAll, instance, typeConstraint);
            declarationStatement.LanguageService.CodeFormatter.Format(declarationStatement.ToTreeNode(),
                                                                      CodeFormatProfile.GENERATOR);
            return control => CSharpTemplateUtil.ExecuteTemplate(
                solution,
                control,
                typeConstraint,
                declarationStatement.VariableDeclarations[0].ToTreeNode().TypeUsage,
                true,
                new TextRange(declarationStatement.VariableDeclarations[0].GetNameDocumentRange().TextRange.EndOffset));
        }

        private IReferenceExpression GetReferenceExpression()
        {
            return _myReference == null ? null : (_myReference.GetElement() as IReferenceExpression);
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
            var referenceName = _myReferenceExpression.Reference.GetName();
            var elementsWithUnresolvedReferences = CollectElementsWithUnresolvedReferences(scope, referenceName);

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

        private IDeclarationStatement GetDeclarationStatement(IStatement statementToBeVisibleToAll, CSharpElementFactory factory, IExpectedTypeConstraint typeConstraint)
        {
            try
            {
                var statement = (IDeclarationStatement)factory.CreateStatement(
                    "var $0 = MockRepository.GenerateStub<$1>();",
                    _myReference.GetName(),
                    GetStubInterfaceName(typeConstraint));
                return statement == null 
                    ? null 
                    : StatementUtil.InsertStatement(statement, ref statementToBeVisibleToAll, true);
            }
            catch (Exception ex)
            {
                File.AppendAllText("c:\\temp\\MillimanPluginErrors.txt", "Exception on " + DateTime.Now + "\n" + ex + "\n\n");
                throw;
            }
        }

        private string GetStubInterfaceName(IExpectedTypeConstraint typeConstraint)
        {
            var languageType = _anchor.Language;
            var firstInterfaceType = typeConstraint.GetDefaultTypes().First();
            return firstInterfaceType.GetPresentableName(languageType);
        }
    }
}