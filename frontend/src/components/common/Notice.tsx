import { X } from "lucide-react";

interface NoticeProps {
  title: string;
  body: string;
}

export function Notice({ title, body }: NoticeProps) {
  return (
    <div className="notice-box">
      <X size={20} />
      <div>
        <b>{title}</b>
        <p>{body}</p>
      </div>
    </div>
  );
}

