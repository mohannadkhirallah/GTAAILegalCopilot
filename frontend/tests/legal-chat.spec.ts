import { expect, test, type Route, type Page } from "@playwright/test";

const health = {
  status: "ok",
  aiExtractionEnabled: false,
  aiNarrativeEnabled: false,
  legalSearchEnabled: true,
  legalChatEnabled: true,
};
const source = {
  id: "test-law-1",
  citationId: "S1",
  officialTitle: "قانون تجريبي لعرض المصادر",
  number: "24",
  year: "2018",
  articleNumber: "18",
  status: "قيد التطبيق",
  sourceUrl: "https://www.almeezan.qa/example",
  content: "نص مسترجع تجريبي لأغراض اختبار الواجهة.",
};


async function openDossier(page: Page) {
  await page.goto("/");
  await expect(page.locator("#workspace-chat")).toHaveCount(0);
  await page.getByRole("button", { name: /حالة «الدوحة تك»/ }).click();
  await page.getByRole("button", { name: "اسأل عن هذا الملف" }).click();
  await expect(page.getByRole("heading", { name: "المستشار القانوني", exact: true })).toBeVisible();
}

async function respond(route: Route, answer: { answer: string; sources: typeof source[]; grounded: boolean }) {
  await route.fulfill({
    contentType: "text/event-stream; charset=utf-8",
    body: `event: delta\ndata: ${JSON.stringify({ text: answer.answer })}\n\nevent: done\ndata: ${JSON.stringify(answer)}\n\n`,
  });
}

test("assistant drawer formats Markdown and keeps the conversation when closed", async ({ page }) => {
  await page.route("**/api/health", route => route.fulfill({ json: health }));
  await page.route("**/api/dossiers/*/chat/stream", route => respond(route, {
    answer: "### ملخص الملف\n\n**المبلغ المطالب به:** 4,000,000 ريال قطري.\n\n- البند الأول\n- البند الثاني [S1]\n\n| البند | القيمة |\n| --- | --- |\n| المطالبة | 4,000,000 |\n\n<script>window.bad = true</script>\n[رابط غير معتمد](https://example.com)",
    sources: [source], grounded: true,
  }));
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("لخص الملف");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.locator(".chat-markdown h3")).toHaveText("ملخص الملف");
  await expect(page.locator(".chat-markdown strong")).toHaveText("المبلغ المطالب به:");
  await expect(page.locator(".chat-markdown li")).toHaveCount(2);
  await expect(page.locator(".chat-markdown table")).toHaveCount(1);
  await expect(page.locator(".chat-markdown script")).toHaveCount(0);
  await expect(page.locator('.chat-markdown a[href="https://example.com"]')).toHaveCount(0);
  await page.getByRole("link", { name: "عرض المصدر S1" }).click();
  await expect(page.getByRole("link", { name: "فتح المصدر" })).toBeVisible();
  await page.getByRole("button", { name: "إغلاق المساعد" }).click();
  await expect(page.locator("#workspace-chat")).toBeHidden();
  await page.getByRole("button", { name: "المستشار القانوني", exact: true }).click();
  await expect(page.locator(".chat-markdown h3")).toHaveText("ملخص الملف");
  await page.setViewportSize({ width: 390, height: 844 });
  const bounds = await page.locator("#workspace-chat").boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(390);
});

test("chat streams text before completion and attaches sources only when done", async ({ page }) => {
  await page.route("**/api/health", route => route.fulfill({ json: health }));
  await openDossier(page);
  await page.evaluate((finalSource) => {
    const original = window.fetch.bind(window);
    window.fetch = async (input, init) => {
      if (!String(input).endsWith("/chat/stream")) return original(input, init);
      return new Response(new ReadableStream({
        start(controller) {
          const encoder = new TextEncoder();
          controller.enqueue(encoder.encode('event: delta\ndata: {"text":"إجابة جزئية"}\n\n'));
          Object.assign(window, {
            finishChatStream: () => {
              const answer = { answer: "إجابة مكتملة [S1]", sources: [finalSource], grounded: true };
              controller.enqueue(encoder.encode(`event: done\ndata: ${JSON.stringify(answer)}\n\n`));
              controller.close();
            },
          });
        },
      }), { headers: { "Content-Type": "text/event-stream" } });
    };
  }, source);
  await page.getByLabel("سؤالك عن الملف").fill("اشرح القانون");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("إجابة جزئية", { exact: true })).toBeVisible();
  await expect(page.locator(".chat-sources")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "إيقاف", exact: true })).toBeVisible();
  await page.evaluate(() => (window as Window & { finishChatStream: () => void }).finishChatStream());
  await expect(page.getByText("إجابة مكتملة")).toBeVisible();
  await expect(page.locator(".chat-sources")).toHaveCount(1);
  await expect(page.getByRole("button", { name: "إرسال السؤال" })).toBeVisible();
});

test("greetings display normally without sources and remain in legal follow-up history", async ({ page }) => {
  await page.route("**/api/health", route => route.fulfill({ json: health }));
  let calls = 0;
  await page.route("**/api/dossiers/*/chat/stream", async route => {
    if (++calls === 1) {
      await respond(route, { answer: "مرحباً! كيف يمكنني مساعدتك؟", sources: [], grounded: false });
    } else {
      expect(route.request().postDataJSON().history).toEqual([
        { role: "user", text: "hello" },
        { role: "assistant", text: "مرحباً! كيف يمكنني مساعدتك؟" },
      ]);
      await respond(route, { answer: "شرح قانوني [S1]", sources: [source], grounded: true });
    }
  });
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("hello");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("مرحباً! كيف يمكنني مساعدتك؟")).toBeVisible();
  await expect(page.locator(".chat-sources")).toHaveCount(0);
  await page.getByLabel("سؤالك عن الملف").fill("اشرح الاعتراض");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.locator(".chat-sources")).toHaveCount(1);
});

test("switching dossiers starts a separate conversation with no previous-file history", async ({ page }) => {
  await page.route("**/api/health", route => route.fulfill({ json: health }));
  const requests: { url: string; history: unknown[] }[] = [];
  await page.route("**/api/dossiers/*/chat/stream", async route => {
    requests.push({ url: route.request().url(), history: route.request().postDataJSON().history });
    await respond(route, { answer: "بحسب الملف الحالي", sources: [], grounded: false });
  });
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("ما اسم الشركة؟");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("بحسب الملف الحالي", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: /حالة «سيمنز»/ }).click();
  await page.getByRole("button", { name: "اسأل عن هذا الملف" }).click();
  await expect(page.locator("#workspace-chat .chat-turn")).toHaveCount(0);
  await expect(page.getByLabel("سؤالك عن الملف")).toBeEnabled();
  await page.getByLabel("سؤالك عن الملف").fill("ما اسم الشركة؟");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("بحسب الملف الحالي", { exact: true })).toBeVisible();
  expect(requests[0].url).toContain("/DEMO-DOHATECH/chat/stream");
  expect(requests[1].url).toContain("/DEMO-SIEMENS/chat/stream");
  expect(requests[1].history).toEqual([]);
});

test("chat shows source text, sends follow-up history, within the selected dossier", async ({
  page,
}) => {
  await page.route("**/api/health", (route) => route.fulfill({ json: health }));
  const requests: {
    message: string;
    history: { role: string; text: string }[];
  }[] = [];
  await page.route("**/api/dossiers/*/chat/stream", async (route) => {
    requests.push(route.request().postDataJSON());
    await respond(route, {
        answer: "إجابة تجريبية موثقة [S1]",
        sources: [source],
        grounded: true,
      });
  });
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("اشرح الاعتراض");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("إجابة تجريبية موثقة")).toBeVisible();
  await page.getByRole("link", { name: "عرض المصدر S1" }).click();
  await page.getByText("عرض النص المسترجع").click();
  await expect(page.getByText(source.content)).toBeVisible();
  await expect(page.getByRole("link", { name: "فتح المصدر" })).toHaveAttribute(
    "href",
    source.sourceUrl,
  );
  await expect(page.getByText("إجابة تجريبية موثقة")).toBeVisible();
  await page.getByLabel("سؤالك عن الملف").fill("وما الموعد؟");
  await page.getByLabel("سؤالك عن الملف").press("Enter");
  await expect(page.locator(".chat-turn-assistant")).toHaveCount(2);
  expect(requests[1].history).toEqual([
    { role: "user", text: "اشرح الاعتراض" },
    { role: "assistant", text: "إجابة تجريبية موثقة [S1]" },
  ]);
  await page.getByRole("button", { name: "محادثة جديدة" }).click();
  await expect(page.locator(".chat-turn")).toHaveCount(0);
});

test("failed chat restores the question and does not add it to subsequent history", async ({
  page,
}) => {
  await page.route("**/api/health", (route) => route.fulfill({ json: health }));
  let calls = 0;
  await page.route("**/api/dossiers/*/chat/stream", async (route) => {
    if (++calls === 1) {
      await route.fulfill({
        status: 503,
        json: { title: "خدمة البحث غير متاحة" },
      });
    } else {
      expect(route.request().postDataJSON().history).toEqual([]);
      await respond(route, { answer: "لم أجد نصاً كافياً.", sources: [], grounded: false });
    }
  });
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("سؤال تجريبي");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByRole("alert")).toContainText("خدمة البحث غير متاحة");
  await expect(page.getByLabel("سؤالك عن الملف")).toHaveValue("سؤال تجريبي");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(page.getByText("لم أجد نصاً كافياً.")).toBeVisible();
  await expect(page.locator(".chat-sources")).toHaveCount(0);
});

test("chat is disabled when services are unconfigured and fits a mobile viewport", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openDossier(page);
  await expect(page.getByLabel("سؤالك عن الملف")).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "إرسال السؤال" }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});

test("the existing demo still analyzes, generates a memo, and downloads Word", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: /حالة «الدوحة تك»/ }).click();
  await expect(page.getByText("مقبول شكلاً", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "إعداد المذكرة", exact: true })
    .click();
  await expect(page.getByRole("link", { name: "تصدير Word" })).toBeVisible();
  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("link", { name: "تصدير Word" }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.docx$/);
});

test("new conversation cancels a pending request without restoring stale content", async ({
  page,
}) => {
  await page.route("**/api/health", (route) => route.fulfill({ json: health }));
  await page.route("**/api/dossiers/*/chat/stream", async () => {
    /* Hold the request until the UI cancels it. */
  });
  await openDossier(page);
  await page.getByLabel("سؤالك عن الملف").fill("سؤال طويل البحث");
  await page.getByRole("button", { name: "إرسال السؤال" }).click();
  await expect(
    page.getByRole("button", { name: "إيقاف", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "محادثة جديدة" }).click();
  await expect(page.locator(".chat-turn")).toHaveCount(0);
  await expect(page.getByLabel("سؤالك عن الملف")).toBeEnabled();
  await expect(page.getByLabel("سؤالك عن الملف")).toHaveValue("");
});
