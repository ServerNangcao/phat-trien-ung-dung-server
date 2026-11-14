import { z } from "zod";

export const loginSchema = z.object({
  email: z.string().trim().min(1, "Vui lòng nhập email.").email("Email chưa đúng định dạng."),
  password: z.string().min(1, "Vui lòng nhập mật khẩu."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const registerSchema = z
  .object({
    fullName: z.string().trim().min(2, "Vui lòng nhập họ tên (ít nhất 2 ký tự)."),
    email: z.string().trim().min(1, "Vui lòng nhập email.").email("Email chưa đúng định dạng."),
    password: z.string().min(8, "Mật khẩu cần có ít nhất 8 ký tự."),
    confirmPassword: z.string().min(1, "Vui lòng xác nhận mật khẩu."),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: "Mật khẩu xác nhận chưa khớp.",
    path: ["confirmPassword"],
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;

