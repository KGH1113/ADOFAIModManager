import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { secureUrl } from "@/models/catalog/catalog-model";
export function ModDescription({ text, openUrl }: { text: string; openUrl(url: string): void }) {
  const normalized = text
    .replace(/<a?:([\w]+):\d+>/g, ":$1:")
    .replace(/<@!?\d+>/g, "@user")
    .replace(/<#\d+>/g, "#channel");
  return (
    <div className="mod-description">
      <Markdown
        remarkPlugins={[remarkGfm]}
        skipHtml
        urlTransform={(url) => secureUrl(url) ?? ""}
        components={{
          a: ({ href, children }) =>
            href ? (
              <a
                href={href}
                onClick={(event) => {
                  event.preventDefault();
                  openUrl(href);
                }}
              >
                {children}
              </a>
            ) : (
              <span>{children}</span>
            ),
          img: ({ alt }) => (alt ? <span className="text-muted-foreground">[{alt}]</span> : null),
        }}
      >
        {normalized}
      </Markdown>
    </div>
  );
}
