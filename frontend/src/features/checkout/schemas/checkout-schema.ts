import { z } from "zod";

export const checkoutSchema = z.object({
  name: z.string().trim().min(2, "Vui lòng nhập họ tên."),
  email: z.string().trim().email("Email chưa đúng định dạng."),
  phone: z.string().trim().regex(/^(0|\+84)[0-9]{9,10}$/, "Số điện thoại chưa đúng định dạng."),
  accepted: z.literal(true, { error: "Vui lòng đồng ý với điều khoản." }),
});

export type CheckoutFormValues = z.infer<typeof checkoutSchema>;

