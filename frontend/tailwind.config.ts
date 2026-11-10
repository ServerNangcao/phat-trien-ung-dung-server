import type { Config } from "tailwindcss";

export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#1d2735",
        paper: "#f8f6f1",
        coral: "#f05b45",
        "coral-dark": "#d94735",
        mint: "#d9eee2",
        butter: "#f5e8b9",
      },
      fontFamily: {
        sans: ["DM Sans", "sans-serif"],
        display: ["Manrope", "sans-serif"],
      },
      boxShadow: {
        card: "0 12px 32px rgba(29, 39, 53, .07)",
      },
    },
  },
  plugins: [require("@tailwindcss/forms"), require("@tailwindcss/typography")],
} satisfies Config;
