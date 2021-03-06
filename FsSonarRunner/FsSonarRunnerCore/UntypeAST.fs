﻿// This code borrowed from https://github.com/fsprojects/VisualFSharpPowerTools/
namespace FsSonarRunnerCore

module UntypedAstUtils =
    open System

    open Microsoft.FSharp.Compiler.Ast   
    open Microsoft.FSharp.Compiler
    open Microsoft.FSharp.Compiler.SourceCodeServices

    type TokenData() =
        member val Line : int = 0 with get, set
        member val LeftColoumn : int = 0 with get, set
        member val RightColoumn : int = 0 with get, set
        member val Type : string = "" with get, set
        member val Content : string = "" with get, set

    type internal ShortIdent = string
    type internal Idents = ShortIdent[]

    let internal longIdentToArray (longIdent: LongIdent): Idents =
        longIdent |> List.map string |> List.toArray

    let getDumpToken (content : string [])  = 
        let sourceTok = FSharpSourceTokenizer([], "C:\\test.fsx")
        let mutable tokens = List.empty

        let chop (input : string) len = 
            Array.init (input.Length / len) (fun index ->
            let start = index * len
            input.[start..start + len - 1])

        let createTokenData(line : int, tok : FSharpTokenInfo, extractStr : bool) =
            let data = new TokenData(Line = line, LeftColoumn = tok.LeftColumn, RightColoumn = tok.RightColumn, Type = tok.TokenName)
            if extractStr then
                data.Content <- content.[line - 1].Substring(tok.LeftColumn, tok.RightColumn - tok.LeftColumn + 1)
            else
                data.Content <- "\"\""
            data

        let rec tokenizeLine (tokenizer:FSharpLineTokenizer) state count =
            match tokenizer.ScanToken(state) with
            | Some tok, state ->
                // Print token name
                match tok.TokenName with
                | "LINE_COMMENT"
                | "WHITESPACE" -> ()
                | "STRING"
                | "STRING_TEXT" ->
                    if not(tok.LeftColumn.Equals(tok.RightColumn)) then
                        
                        tokens <- tokens @ [(tok, count)]
                | str -> tokens <- tokens @ [(tok, count)]

                // Tokenize the rest, in the new state
                tokenizeLine tokenizer state count
            | None, state -> state


        let rec tokenizeLines state count lines = 
            match lines with
            | line::lines ->
                // Create tokenizer & tokenize single line
                let tokenizer = sourceTok.CreateLineTokenizer(line)
                let state = tokenizeLine tokenizer state count
                // Tokenize the rest using new state
                tokenizeLines state (count+1) lines
            | [] -> ()

        content
        |> List.ofSeq
        |> tokenizeLines 0L 1 

        let mutable tokensSimple : TokenData List = List.empty
        
        // remove duplicated string text
        let mutable dontAddNext = false
        tokens |> Seq.iteri (fun i (tok, count) ->
            match tok.TokenName with
                | "STRING"
                | "STRING_TEXT" ->
                    if dontAddNext = false then
                        tokensSimple <- tokensSimple @ [createTokenData(count, tok, false)]

                    try
                        let nexttoken, cnt = tokens.[i+1]                    
                        match nexttoken.TokenName with
                        | "STRING"
                        | "STRING_TEXT" -> 
                            tokensSimple.[tokensSimple.Length-1].RightColoumn <- nexttoken.RightColumn
                            dontAddNext <- true
                        | _ ->  dontAddNext <- false
                    with
                    | _ -> ()
                | _ -> 
                    tokensSimple <- tokensSimple @ [createTokenData(count, tok, true)]
                    dontAddNext <- false
                    )
                                    
        tokensSimple

    /// Returns ranges of all code lines in AST
    let getCodeMetrics ast =
        let mutable uniqueLines = Set.empty
        let mutable nmbClass = 0
        let mutable autoProperties = 0
        let mutable functions = 0
        let mutable complexity = 0
        let mutable functionComplexityDist = ""

        let mutable functionNodesRanges = Set.empty
        let mutable functionNodes = List.Empty

        let addToUniqueRange(range : Range.range) =
            if not(uniqueLines.Contains(range.StartLine)) then
                uniqueLines <- uniqueLines.Add(range.StartLine)

        let addToUniqueFunctions(range : Range.range, binding) =
            if not(functionNodesRanges.Contains(range.StartLine)) then
                functionNodesRanges <- functionNodesRanges.Add(range.StartLine)
                functionNodes <- functionNodes @ [binding]

        let rec visitExpr = function

            // complexity increase
            | SynExpr.IfThenElse(cond, trueBranch, falseBranchOpt, _, _, _, range) ->
                complexity <- complexity + 1
                addToUniqueRange(range)
                visitExpr cond
                visitExpr trueBranch
                falseBranchOpt |> Option.iter visitExpr

            | SynExpr.LetOrUse (_, _, bindings, body, range) -> 
                functions <- functions + 1
                addToUniqueRange(range)
                visitBindindgs bindings
                visitExpr body

            | SynExpr.LetOrUseBang (_, _, _, _, rhsExpr, body, range) -> 
                functions <- functions + 1
                addToUniqueRange(range)
                visitExpr rhsExpr
                visitExpr body

            | SynExpr.App (_,_, funcExpr, argExpr, range) ->
                addToUniqueRange(range)
                visitExpr argExpr
                visitExpr funcExpr
            
            | SynExpr.Lambda (_, _, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.Record (_, _, fields, range) -> 
                addToUniqueRange(range)
                fields |> List.choose (fun (_, expr, _) -> expr) |> List.iter visitExpr
            
            | SynExpr.ArrayOrListOfSeqExpr (_, expr, range) -> 
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.CompExpr (_, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            // complexity increase
            | SynExpr.ForEach (_, _, _, _, _, body, range) ->
                complexity <- complexity + 1
                addToUniqueRange(range)
                visitExpr body
            
            | SynExpr.YieldOrReturn (_, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.YieldOrReturnFrom (_, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.Do (expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.DoBang (expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.Downcast (expr, _, range) ->
                addToUniqueRange(range)
                visitExpr expr

            // complexity increase
            | SynExpr.For (_, _, _, _, _, expr, range) ->
                complexity <- complexity + 1
                addToUniqueRange(range)
                visitExpr expr
            
            | SynExpr.Lazy (expr, range) ->
                addToUniqueRange(range)
                visitExpr expr
            
            // complexity increase
            | SynExpr.Match (_, expr, clauses, _, range) -> 
                complexity <- complexity + clauses.Length - 1
                addToUniqueRange(range)
                visitExpr expr
                visitMatches clauses 
            
            // apparently this also increases complexity
            | SynExpr.Ident(ident)
                    when 
                        ident.idText = "op_BooleanAnd" || 
                        ident.idText = "op_BooleanOr" -> complexity <- complexity + 1

            | SynExpr.MatchLambda (_, _, clauses, _, range) ->
                addToUniqueRange(range)
                visitMatches clauses
            
            | SynExpr.ObjExpr (_, _, bindings, _, _ , range) -> 
                addToUniqueRange(range)
                visitBindindgs bindings
            
            | SynExpr.Typed (expr, _, range) -> 
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.Paren (expr, _, _, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.Sequential (_, _, expr1, expr2, range) ->
                addToUniqueRange(range)
                visitExpr expr1
                visitExpr expr2

            | SynExpr.LongIdentSet (_, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.Tuple (exprs, _, range) -> 
                addToUniqueRange(range)
                for expr in exprs do 
                    visitExpr expr

            | SynExpr.TryFinally (expr1, expr2, range, _, _) ->
                addToUniqueRange(range)                
                visitExpr expr1
                visitExpr expr2

            | SynExpr.TryWith (expr, range, clauses, range1, range2, _, _) ->
                addToUniqueRange(range)
                addToUniqueRange(range1)
                addToUniqueRange(range2)
                visitExpr expr
                visitMatches clauses

            | SynExpr.ArrayOrList(_, exprs, range) ->
                addToUniqueRange(range)
                List.iter visitExpr exprs

            | SynExpr.New(_, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            // complexity increase
            | SynExpr.While(_, expr1, expr2, range) -> 
                addToUniqueRange(range)
                visitExpr expr1
                visitExpr expr2

            | SynExpr.Assert(expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.TypeApp(expr, range, _, rangelist, rangeoption, range1, range2) ->
                addToUniqueRange(range)
                addToUniqueRange(range1)
                addToUniqueRange(range2)
                rangelist |> Seq.iter (fun elem -> addToUniqueRange(elem))
                match rangeoption with
                | Some value -> addToUniqueRange(value)
                | _ -> ()
                visitExpr expr

            | SynExpr.DotSet(_, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.DotIndexedSet(_, _, expr, range, range1, range2) ->
                addToUniqueRange(range)
                addToUniqueRange(range1)
                addToUniqueRange(range2)
                visitExpr expr

            | SynExpr.NamedIndexedPropertySet(_, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.DotNamedIndexedPropertySet(_, _, _, expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.TypeTest(expr, _, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.Upcast(expr, _, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.InferredUpcast(expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.InferredDowncast(expr, range) ->
                addToUniqueRange(range)
                visitExpr expr

            | SynExpr.AddressOf(_, expr, range, range1) ->
                addToUniqueRange(range)
                addToUniqueRange(range1)
                visitExpr expr

            | expr -> addToUniqueRange(expr.Range)

        and visitBinding (Binding(_, _, _, _, _, _, _, _, _, body, range, _)) =
            addToUniqueRange(range)
            visitExpr body
        and visitBindindgs = List.iter visitBinding
        and visitMatch (SynMatchClause.Clause (_, _, expr, range, _)) =
            addToUniqueRange(range)
            visitExpr expr
        and visitMatches = List.iter visitMatch
        
        let visitMember = function
            | SynMemberDefn.LetBindings (bindings, _, _, range) ->
                functions <- functions + 1
                addToUniqueFunctions(range, bindings)

                addToUniqueRange(range)
                visitBindindgs bindings
            | SynMemberDefn.Member (binding, range) ->
                functions <- functions + 1
                addToUniqueRange(range)
                visitBinding binding
            | SynMemberDefn.AutoProperty (_, _, _, _, _, _, _, _, expr, _, range) ->
                autoProperties <- autoProperties + 1
                addToUniqueRange(range)
                visitExpr expr
            | SynMemberDefn.ImplicitCtor (_, _, _, _, range) ->
                nmbClass <- nmbClass + 1
                addToUniqueRange(range)
            | _ -> printfn "Not Supported member Report" 

        let rec visitType ty =
            let (SynTypeDefn.TypeDefn (_, repr, _, _)) = ty

            match repr with
            | SynTypeDefnRepr.Simple (defns, range) -> 
                match defns with
                | SynTypeDefnSimpleRepr.Enum(defs, range) -> 
                    for en in defs do
                        addToUniqueRange(en.Range)

                | _ -> ()

            | SynTypeDefnRepr.ObjectModel (_, defns, _) ->
                for d in defns do
                    addToUniqueRange(d.Range)
                    visitMember d


        let rec visitDeclarations decls = 
            for declaration in decls do
                match declaration with
                | SynModuleDecl.Exception (arg1, range) -> addToUniqueRange(range)

                | SynModuleDecl.Let (_, bindings, range) ->
                    functions <- functions + 1
                    addToUniqueFunctions(range, bindings)
                    addToUniqueRange(range)
                    visitBindindgs bindings
                
                | SynModuleDecl.DoExpr (_, expr, _) ->
                    addToUniqueRange(expr.Range)
                    visitExpr expr
                
                | SynModuleDecl.Types (types, range) ->
                    for ty in types do
                        addToUniqueRange(ty.Range)
                        visitType ty

                | SynModuleDecl.Open (_, range) -> addToUniqueRange(range)
                
                | SynModuleDecl.NestedModule (_, decls, _, _) ->                    
                    visitDeclarations decls
                | SynModuleDecl.Attributes (_, range) -> 
                    addToUniqueRange(range)
                | _ -> printfn "Not Supported Declaration: %A" declaration

        let visitModulesAndNamespaces modulesOrNss =
            for moduleOrNs in modulesOrNss do
                let (SynModuleOrNamespace(_, _, decls, _, _, _, _)) = moduleOrNs
                visitDeclarations decls
                
        ast 
        |> Option.iter (function
            | ParsedInput.ImplFile implFile ->
                let (ParsedImplFileInput(_, _, _, _, _, modules, _)) = implFile
                visitModulesAndNamespaces modules
            | _ -> ())


        let fileComplexity = complexity
        let mutable functionComplexities = List.Empty
        for bindings in functionNodes do
            complexity <- 0 
            visitBindindgs bindings
            functionComplexities <- functionComplexities @ [complexity]

        uniqueLines,
        nmbClass,
        autoProperties,
        functions,
        fileComplexity,
        ComplexityDistributions.GetFileComplexityDist(fileComplexity),
        ComplexityDistributions.GetFunctionComplexityDist(functionComplexities)
         
    // get number of lines in file
    let GetLines(ast : ParsedInput option) = 
        match ast with
        | Some data -> data.Range.EndLine - data.Range.StartLine + 1
        | _ -> -1


