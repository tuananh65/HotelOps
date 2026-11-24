import { useEffect } from "react";

/** Translate native constraints without replacing backend validation. */
export function useVietnameseValidation() {
  useEffect(() => {
    const invalid = (event: Event) => {
      const input = event.target;
      if (!(
        input instanceof HTMLInputElement ||
        input instanceof HTMLSelectElement ||
        input instanceof HTMLTextAreaElement
      ))
        return;
      const validity = input.validity;
      if (validity.customError) return;
      let text = "Vui lòng kiểm tra thông tin đã nhập.";
      if (validity.valueMissing)
        text = "Vui lòng nhập hoặc chọn thông tin này.";
      else if (validity.typeMismatch)
        text =
          input instanceof HTMLInputElement && input.type === "email"
            ? "Địa chỉ email không hợp lệ."
            : "Thông tin không đúng định dạng.";
      else if (validity.tooShort)
        text = `Vui lòng nhập ít nhất ${(input as HTMLInputElement).minLength} ký tự.`;
      else if (validity.tooLong)
        text = `Chỉ nhập tối đa ${(input as HTMLInputElement).maxLength} ký tự.`;
      else if (validity.rangeUnderflow)
        text = `Giá trị phải từ ${(input as HTMLInputElement).min} trở lên.`;
      else if (validity.rangeOverflow)
        text = `Giá trị không được vượt quá ${(input as HTMLInputElement).max}.`;
      else if (validity.patternMismatch)
        text = input.title || "Thông tin không đúng định dạng yêu cầu.";
      else if (validity.badInput || validity.stepMismatch)
        text = "Vui lòng nhập số hợp lệ.";
      input.setCustomValidity(text);
      input.dataset.translatedValidity = "true";
    };
    const clear = (event: Event) => {
      const input = event.target as HTMLInputElement;
      if (input?.dataset?.translatedValidity) {
        input.setCustomValidity("");
        delete input.dataset.translatedValidity;
      }
    };
    document.addEventListener("invalid", invalid, true);
    document.addEventListener("input", clear, true);
    document.addEventListener("change", clear, true);
    return () => {
      document.removeEventListener("invalid", invalid, true);
      document.removeEventListener("input", clear, true);
      document.removeEventListener("change", clear, true);
    };
  }, []);
}
