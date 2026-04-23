namespace Starter.Module.AI.Infrastructure.Eval.Faithfulness;

internal static class FaithfulnessJudgePrompts
{
    public const string EnglishPrompt = """
        You are an impartial judge. Given a QUESTION, a CONTEXT, and an ANSWER,
        extract each atomic claim in the ANSWER and classify each as:
          SUPPORTED   — directly stated or clearly inferable from CONTEXT.
          UNSUPPORTED — not stated in CONTEXT.

        Output strict JSON with no prose:
          { "claims": [ { "text": "<claim>", "verdict": "SUPPORTED" | "UNSUPPORTED" } ] }

        QUESTION: {question}
        CONTEXT: {context}
        ANSWER: {answer}
        """;

    public const string ArabicPrompt = """
        أنت حكمٌ محايد. بناءً على السؤال والسياق والإجابة، استخرج كل ادعاء ذري في الإجابة وصنّفه كما يلي:
          SUPPORTED   — مذكور صراحةً أو يمكن استنتاجه بوضوح من السياق.
          UNSUPPORTED — غير مذكور في السياق.

        أخرِج JSON صارم فقط بدون أي نص إضافي:
          { "claims": [ { "text": "<الادعاء>", "verdict": "SUPPORTED" | "UNSUPPORTED" } ] }

        السؤال: {question}
        السياق: {context}
        الإجابة: {answer}
        """;
}
